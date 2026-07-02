using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
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

    public async Task<ResultadosModel<T>?> ObtenerUltimoEstadoAsync<T>(string nombreSitio, string nombreArchivo)
    {
        try
        {
            var clienteContenedor = new BlobContainerClient(_cadenaConexion, _nombreContenedor);
            string rutaBlob = $"{nombreSitio}/{nombreArchivo}";
            var clienteBlob = clienteContenedor.GetBlobClient(rutaBlob);

            if (!await clienteBlob.ExistsAsync())
            {
                return null;
            }

            var respuestaDownload = await clienteBlob.DownloadStreamingAsync();
            using var flujoBlob = respuestaDownload.Value.Content;

            // CAMBIO AQUÍ: Deserializamos directamente al tipo ResultadosModel<T> esperado
            return await JsonSerializer.DeserializeAsync<ResultadosModel<T>>(flujoBlob, _opcionesJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error crítico al descargar el estado desde Azure Blob Storage para el sitio '{nombreSitio}'.", ex);
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
}