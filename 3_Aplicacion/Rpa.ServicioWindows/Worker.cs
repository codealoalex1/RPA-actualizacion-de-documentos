using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rpa.Infraestructura.Azure;
using Rpa.Infraestructura.SitiosWeb;
using Rpa.Infraestructura.Utilidades;
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
    private readonly BCBResoluciones _bcbR;
    private readonly JsonSerializerOptions _opcionesJson;
    private readonly IAlmacenamientoServicio _almacenamiento;

    public Worker(
        ILogger<Worker> logger,
        ImpuestosExtractor impuestosExtractor,
        NormativaMEFP mefpExtractor,
        GODecreto gODecreto,
        GOLeyes gOLeyes,
        BCBCircularesExternas bCBCircularesExternas,
        BCBResoluciones bCBResoluciones,
        IAlmacenamientoServicio almacenamientoBlob
        )
    {
        _logger = logger;
        _impuestosExtractor = impuestosExtractor;
        _mefpExtractor = mefpExtractor;
        _goDecreto = gODecreto;
        _goLeyes = gOLeyes;
        _bcbCE = bCBCircularesExternas;
        _bcbR = bCBResoluciones;
        _almacenamiento = almacenamientoBlob;
        _opcionesJson = new JsonSerializerOptions { WriteIndented = true };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        /* _logger.LogInformation("Servicio RPA en Azure Container Apps Iniciado.");

        // --- 3. PROCESAR GACETA DECRETOS ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _goDecreto.NombreSitio);
            var goDecreto = await _goDecreto.ExtraerDatosAsync(1);
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
            var goLeyes = await _goLeyes.ExtraerDatosAsync(1);
            await _almacenamiento.GuardarEstadoAsync<GOLeyModel>(goLeyes, _goLeyes.NombreSitio, $"documento_{_goLeyes.NombreSitio}.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Gaceta Leyes.");
        } */
        // --- 1. PROCESAR IMPUESTOS ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _impuestosExtractor.NombreSitio);
            var modeloVacio = new ResultadosModel<ImpuestosModel>();

            var estadoActual = await _almacenamiento.ObtenerUltimoEstadoAsync<ImpuestosModel>(_impuestosExtractor.NombreSitio, "nuevo.json");

            long criterio = GestorEstadoRpa.ObtenerFechaCriterio<ImpuestosModel>(
                estadoActual,
                x => x.Fecha,
                modeloVacio.convertirHora
            );

            Console.WriteLine(new DateTime(criterio));

            string identificador = estadoActual.ContenidoIdentificado.Find(x=>modeloVacio.convertirHora(x.Fecha)==criterio).Titulo;
            var resImpuestos = await _impuestosExtractor.ExtraerDatosAsync(criterio, identificador);
            if (resImpuestos.ContenidoIdentificado.Count > 0)
            {
                await _almacenamiento.GuardarYRotarEstadoAsync<ImpuestosModel>(resImpuestos, _impuestosExtractor.NombreSitio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en Impuestos.");
        }

        // --- 2. PROCESAR MEFP ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _mefpExtractor.NombreSitio);
            var modeloVacio = new ResultadosModel<MEFPModel>();

            var estadoActual = await _almacenamiento.ObtenerUltimoEstadoAsync<MEFPModel>(_mefpExtractor.NombreSitio, "nuevo.json");

            long criterio = GestorEstadoRpa.ObtenerFechaCriterio<MEFPModel>(
                estadoActual,
                x => x.FechaPublicacion,
                modeloVacio.convertirHora
            );
            string identificador = estadoActual.ContenidoIdentificado.Find(x=>modeloVacio.convertirHora(x.FechaPublicacion)==criterio).Titulo;

            var resMefp = await _mefpExtractor.ExtraerDatosAsync(criterio, identificador);
            if (resMefp.ContenidoIdentificado.Count > 0)
            {
                await _almacenamiento.GuardarYRotarEstadoAsync<MEFPModel>(resMefp, _mefpExtractor.NombreSitio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MEFP.");
        }

        // --- 5. PROCESAR BCB CIRCULARES ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _bcbCE.NombreSitio);
            var modeloVacio = new ResultadosModel<BCBCircularesExternasModel>();

            // REUTILIZACIÓN: Llamamos al mismo servicio con otro modelo
            var estadoActual = await _almacenamiento.ObtenerUltimoEstadoAsync<BCBCircularesExternasModel>(_bcbCE.NombreSitio, "nuevo.json");

            // LLAMADA AL GESTOR GENERAL: Cambiando únicamente el tipo de modelo
            long criterio = GestorEstadoRpa.ObtenerFechaCriterio<BCBCircularesExternasModel>(
                estadoActual,
                x => x.Fecha,
                modeloVacio.convertirHora
            );

            string identificador = estadoActual.ContenidoIdentificado.Find(x=>modeloVacio.convertirHora(x.Fecha)==criterio).Titulo;
            var bcbCircExternas = await _bcbCE.ExtraerDatosAsync(criterio, identificador);

            if (bcbCircExternas.ContenidoIdentificado.Count > 0)
            {
                await _almacenamiento.GuardarYRotarEstadoAsync<BCBCircularesExternasModel>(bcbCircExternas, _bcbCE.NombreSitio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grave en BCB Circulares.");
        }

        // --- 6. PROCESAR BCB RESOLUCIONES ---
        try
        {
            _logger.LogInformation("Ejecutando: [{sitio}]", _bcbR.NombreSitio);
            var modeloVacio = new ResultadosModel<BCBResolucionesModel>();

            // REUTILIZACIÓN: Llamamos al mismo servicio con otro modelo
            var estadoActual = await _almacenamiento.ObtenerUltimoEstadoAsync<BCBResolucionesModel>(_bcbR.NombreSitio, "nuevo.json");

            // LLAMADA AL GESTOR GENERAL: Cambiando únicamente el tipo de modelo
            long criterio = GestorEstadoRpa.ObtenerFechaCriterio<BCBResolucionesModel>(
                estadoActual,
                x => x.FechaPublicacion,
                modeloVacio.convertirHora
            );

            string identificador = estadoActual.ContenidoIdentificado.Find(x=>modeloVacio.convertirHora(x.FechaPublicacion)==criterio).Resolucion;

            var bcbResoluciones = await _bcbR.ExtraerDatosAsync(criterio, identificador);

            if (bcbResoluciones.ContenidoIdentificado.Count > 0)
            {
                await _almacenamiento.GuardarYRotarEstadoAsync<BCBResolucionesModel>(bcbResoluciones, _bcbR.NombreSitio);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grave en BCB Circulares.");
        }

        _logger.LogInformation("=== Fin del ciclo único de extracción. Terminando contenedor de forma limpia. ===");
        Environment.Exit(0);
    }
}