using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.Azure;

public class AlmacenamientoBlob : IAlmacenamientoServicio
{
    private readonly string _cadenaConexion;
    private readonly string _nombreContenedor;
    private readonly JsonSerializerOptions _opcionesJson;

    // El constructor recibe los parámetros de configuración necesarios para conectar con Azure
    public AlmacenamientoBlob(string cadenaConexion, string nombreContenedor)
    {
        _cadenaConexion = cadenaConexion ?? throw new ArgumentNullException(nameof(cadenaConexion));
        _nombreContenedor = nombreContenedor ?? throw new ArgumentNullException(nameof(nombreContenedor));

        // Configuramos la serialización estándar para todo el proyecto
        _opcionesJson = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // Soluciona el problema de mapeo de minúsculas/mayúsculas
            WriteIndented = true                // Formatea el JSON de manera profesional y legible (Pretty Print)
        };
    }

    public async Task<ResultadosModel<T>?> ObtenerUltimoEstadoAsync<T>(string nombreSitio, string nombreArchivo = "")
    {
        try
        {
            var clienteContenedor = new BlobContainerClient(_cadenaConexion, _nombreContenedor);

            // Definimos el prefijo de la carpeta (ej: "GacetaDecretos/")

            var estadosEncontrados = new List<ResultadosModel<T>>();

            // CORRECCIÓN: Pasamos explícitamente los Traits, States y CancellationToken por defecto exigidos por tu versión del SDK
            var listaBlobs = clienteContenedor.GetBlobsAsync(
                traits: BlobTraits.None,
                states: BlobStates.None,
                prefix: nombreSitio,
                cancellationToken: CancellationToken.None
            );

            await foreach (BlobItem itemInBlob in listaBlobs)
            {
                // Validamos que sea un archivo .json
                if (!itemInBlob.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var clienteBlob = clienteContenedor.GetBlobClient(itemInBlob.Name);

                try
                {
                    var respuestaDownload = await clienteBlob.DownloadStreamingAsync();
                    using var flujoBlob = respuestaDownload.Value.Content;

                    var modeloDeserializado = await JsonSerializer.DeserializeAsync<ResultadosModel<T>>(flujoBlob, _opcionesJson);

                    if (modeloDeserializado != null)
                    {
                        estadosEncontrados.Add(modeloDeserializado);
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[Storage] Advertencia: El archivo '{itemInBlob.Name}' no pudo ser deserializado. {ex.Message}");
                }
            }

            // Si no se encontró ningún archivo .json válido en el contenedor, devolvemos null
            if (!estadosEncontrados.Any())
            {
                return null;
            }

            // Ordenamos por FechaVerificacion de forma descendente y tomamos el primero (el más reciente)
            var masReciente = estadosEncontrados
                .OrderByDescending(x => x.FechaVerificacion)
                .FirstOrDefault();

            if (masReciente != null)
            {
                Console.WriteLine($"[Storage] Analizados {estadosEncontrados.Count()} archivos JSON. Cargando el más reciente con FechaVerificacion: {masReciente.FechaVerificacion}");
            }

            return masReciente;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error crítico al escanear e identificar el estado .json más reciente desde Azure Blob Storage para el sitio '{nombreSitio}'.", ex);
        }
    }

    public async Task GuardarEstadoAsync<T>(ResultadosModel<T> datos, string nombreSitio, string nombreArchivo)
    {
        try
        {
            var clienteContenedor = new BlobContainerClient(_cadenaConexion, _nombreContenedor);

            // Asegurar que el contenedor exista en la cuenta de Azure, si no, lo crea automáticamente
            await clienteContenedor.CreateIfNotExistsAsync();

            string rutaBlob = $"{nombreSitio}/{nombreArchivo}";
            var clienteBlob = clienteContenedor.GetBlobClient(rutaBlob);

            // Convertimos nuestro objeto estructurado a memoria binaria UTF-8
            using var flujoMemoria = new MemoryStream();
            await JsonSerializer.SerializeAsync(flujoMemoria, datos, _opcionesJson);
            flujoMemoria.Position = 0; // Reseteamos la posición del puntero para la lectura de subida

            // Subimos el archivo a Azure, sobreescribiendo el anterior si ya existe (upload: true)
            await clienteBlob.UploadAsync(flujoMemoria, overwrite: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error crítico al subir el nuevo estado hacia Azure Blob Storage para el sitio '{nombreSitio}'.", ex);
        }
    }
    public async Task GuardarYRotarEstadoAsync<T>(ResultadosModel<T> nuevosResultados, string nombreSitio)
    {
        try
        {
            var clienteContenedor = new BlobContainerClient(_cadenaConexion, _nombreContenedor);

            // Construimos las rutas jerárquicas en el Storage (ej: GacetaDecretos/nuevo.json)
            string rutaBlobNuevo = $"{nombreSitio}/nuevo.json";
            string rutaBlobAntiguo = $"{nombreSitio}/antiguo.json";

            var blobNuevoClient = clienteContenedor.GetBlobClient(rutaBlobNuevo);
            var blobAntiguoClient = clienteContenedor.GetBlobClient(rutaBlobAntiguo);

            // 1. ROTACIÓN: Si ya existe un "nuevo.json", lo clonamos/movemos a "antiguo.json"
            if (await blobNuevoClient.ExistsAsync())
            {
                var operacionCopia = await blobAntiguoClient.StartCopyFromUriAsync(blobNuevoClient.Uri);

                await operacionCopia.WaitForCompletionAsync();

                await blobNuevoClient.DeleteAsync();
                Console.WriteLine($"[Storage] Estado anterior rotado con éxito a: {rutaBlobAntiguo}");
            }

            using var flujoMemoria = new MemoryStream();
            await JsonSerializer.SerializeAsync(flujoMemoria, nuevosResultados, _opcionesJson);

            flujoMemoria.Position = 0;

            await blobNuevoClient.UploadAsync(flujoMemoria, overwrite: true);
            Console.WriteLine($"[Storage] Nuevo estado guardado con éxito en: {rutaBlobNuevo}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error crítico al rotar y guardar el estado en Azure Blob Storage para el sitio '{nombreSitio}'.", ex);
        }
    }
    public async Task<bool> EliminarEstadoPorFechaAsync<T>(string nombreSitio, DateTime fechaObjetivo)
    {
        try
        {
            var clienteContenedor = new BlobContainerClient(_cadenaConexion, _nombreContenedor);

            string? rutaBlobAEliminar = null;

            // 1. Listar los blobs estrictamente tipados de la carpeta del sitio
            var listaBlobs = clienteContenedor.GetBlobsAsync(
                traits: BlobTraits.None,
                states: BlobStates.None,
                prefix: nombreSitio,
                cancellationToken: CancellationToken.None
            );

            await foreach (BlobItem itemInBlob in listaBlobs)
            {
                if (!itemInBlob.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var clienteBlob = clienteContenedor.GetBlobClient(itemInBlob.Name);

                try
                {
                    // 2. Descarga parcial en memoria para verificar la metadata interna (FechaVerificacion)
                    var respuestaDownload = await clienteBlob.DownloadStreamingAsync();
                    using var flujoBlob = respuestaDownload.Value.Content;

                    var modeloDeserializado = await JsonSerializer.DeserializeAsync<ResultadosModel<T>>(flujoBlob, _opcionesJson);

                    // 3. Comparamos la FechaVerificacion (usando tolerancia de segundos si es necesario o directa)
                    if (modeloDeserializado != null && modeloDeserializado.FechaVerificacion.Ticks == fechaObjetivo.Ticks)
                    {
                        Console.WriteLine("hola entro");
                        rutaBlobAEliminar = itemInBlob.Name;
                        break; // Encontramos el archivo exacto, salimos del bucle de escaneo
                    }
                }
                catch (JsonException)
                {
                    // Ignorar JSON corruptos durante el escaneo de eliminación
                    continue;
                }
            }

            // 4. Si se identificó la ruta del archivo que cumple el criterio, se procede al borrado
            if (!string.IsNullOrEmpty(rutaBlobAEliminar))
            {
                var blobAEliminarClient = clienteContenedor.GetBlobClient(rutaBlobAEliminar);

                // Borra el archivo y cualquier snapshot asociado si existiera
                var resultadoBorrado = await blobAEliminarClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots);

                if (resultadoBorrado.Value)
                {
                    Console.WriteLine($"[Storage] Archivo eliminado exitosamente: {rutaBlobAEliminar} con FechaVerificacion: {fechaObjetivo}");
                    return true;
                }
            }

            Console.WriteLine($"[Storage] No se encontró ningún archivo .json con la FechaVerificacion especificada en '{nombreSitio}'.");
            return false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error crítico al intentar eliminar un estado por FechaVerificacion para el sitio '{nombreSitio}'.", ex);
        }
    }
}