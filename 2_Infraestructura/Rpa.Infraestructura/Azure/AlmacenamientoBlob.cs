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

            // Construimos la ruta jerárquica (ej: Impuestos_RND_2026/ultimo_estado.json)
            string rutaBlob = $"{nombreSitio}/{nombreArchivo}";
            var clienteBlob = clienteContenedor.GetBlobClient(rutaBlob);

            // Si el archivo no existe en Azure (primera corrida del robot), devolvemos null controladamente
            if (!await clienteBlob.ExistsAsync())
            {
                return null;
            }

            // Descargamos el archivo como un flujo de memoria (Stream)
            var respuestaDownload = await clienteBlob.DownloadStreamingAsync();

            // 2. CORREGIDO: Colocamos el 'using' directamente en el Stream de contenido
            using var flujoBlob = respuestaDownload.Value.Content;

            // 3. Deserializamos usando el flujo de datos limpio
            return await JsonSerializer.DeserializeAsync<dynamic>(flujoBlob, _opcionesJson);
        }
        catch (Exception ex)
        {
            // Lanzamos una excepción personalizada o descriptiva para que el Worker la capture en sus logs
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
}