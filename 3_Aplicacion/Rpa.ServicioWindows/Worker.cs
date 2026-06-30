using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rpa.Infraestructura.Azure;
using Rpa.Infraestructura.SitiosWeb;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.ServicioWindows;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ImpuestosExtractor _impuestosExtractor;
    private readonly NormativaMEFP _mefpExtractor;
    private readonly GODecreto _goDecreto;
    private readonly GOLeyes _goLeyes;
    private readonly BCBCircularesExternas _bcbCE;
    private readonly JsonSerializerOptions _opcionesJson;
    private readonly IAlmacenamientoServicio _almacenamiento;

    public Worker(
        ILogger<Worker> logger,
        ImpuestosExtractor impuestosExtractor,
        NormativaMEFP mefpExtractor,
        GODecreto gODecreto,
        GOLeyes gOLeyes,
        BCBCircularesExternas bCBCircularesExternas,
        IAlmacenamientoServicio almacenamientoBlob
        )
    {
        _logger = logger;
        _impuestosExtractor = impuestosExtractor;
        _mefpExtractor = mefpExtractor;
        _goDecreto = gODecreto;
        _goLeyes = gOLeyes;
        _bcbCE = bCBCircularesExternas;
        _almacenamiento = almacenamientoBlob;
        _opcionesJson = new JsonSerializerOptions { WriteIndented = true };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio RPA en Azure Container Apps Iniciado.");

        // --- AUDITORÍA DE RED INICIAL ---
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "http://www.gacetaoficialdebolivia.gob.bo"), stoppingToken);
            _logger.LogInformation("[AUDITORÍA RED] Conexión exitosa a la Gaceta. Código Estado: {code}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError("[AUDITORÍA RED] Fallo crítico de salida a internet o DNS: {message}", ex.Message);
            if (ex.InnerException != null)
            {
                _logger.LogError("[AUDITORÍA RED] Detalle interno: {inner}", ex.InnerException.Message);
            }
        }

        _logger.LogInformation("=== Iniciando ciclo único de extracción RPA ===");

        // --- 1. PROCESAR IMPUESTOS ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _impuestosExtractor.NombreSitio);
            var resImpuestos = await _impuestosExtractor.ExtraerDatosAsync();
            await _almacenamiento.GuardarEstadoAsync<ImpuestosModel>(resImpuestos, _impuestosExtractor.NombreSitio, $"documento_{_impuestosExtractor.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Impuestos.");
        }

        // --- 2. PROCESAR MEFP ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _mefpExtractor.NombreSitio);
            var resMefp = await _mefpExtractor.ExtraerDatosAsync();
            await _almacenamiento.GuardarEstadoAsync<MEFPModel>(resMefp, _mefpExtractor.NombreSitio, $"documento_{_mefpExtractor.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MEFP.");
        }

        // --- 3. PROCESAR GACETA DECRETOS ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _goDecreto.NombreSitio);
            var goDecreto = await _goDecreto.ExtraerDatosAsync();
            await _almacenamiento.GuardarEstadoAsync<GODecretoModel>(goDecreto, _goDecreto.NombreSitio, $"documento_{_goDecreto.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Gaceta Decretos.");
        }

        // --- 4. PROCESAR GACETA LEYES ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _goLeyes.NombreSitio);
            var goLeyes = await _goLeyes.ExtraerDatosAsync();
            await _almacenamiento.GuardarEstadoAsync<GOLeyModel>(goLeyes, _goLeyes.NombreSitio, $"documento_{_goLeyes.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Gaceta Leyes.");
        }

        // --- 5. PROCESAR BCB CIRCULARES ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _bcbCE.NombreSitio);
            var bcbCircExternas = await _bcbCE.ExtraerDatosAsync();
            await _almacenamiento.GuardarEstadoAsync<BCBCircularesExternasModel>(bcbCircExternas, _bcbCE.NombreSitio, $"documento_{_bcbCE.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grave en BCB Circulares.");
        }

        _logger.LogInformation("=== Fin del ciclo único de extracción. Terminando contenedor de forma limpia. ===");
        Environment.Exit(0);
    }
}