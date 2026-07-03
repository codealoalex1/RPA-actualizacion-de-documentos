using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rpa.Infraestructura.SitiosWeb;
using Rpa.Infraestructura.Utilidades;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.ServicioWindows;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAlmacenamientoServicio _almacenamiento;

    // Tus inyecciones actuales de extractores se mantienen en esta fase...
    private readonly ImpuestosExtractor _impuestosExtractor;
    private readonly NormativaMEFP _mefpExtractor;
    private readonly GODecreto _goDecreto;
    private readonly GOLeyes _goLeyes;
    private readonly BCBCircularesExternas _bcbCE;
    private readonly BCBResoluciones _bcbR;

    public Worker(
        ILogger<Worker> logger,
        ImpuestosExtractor impuestosExtractor,
        NormativaMEFP mefpExtractor,
        GODecreto gODecreto,
        GOLeyes gOLeyes,
        BCBCircularesExternas bCBCircularesExternas,
        BCBResoluciones bCBResoluciones,
        IAlmacenamientoServicio almacenamientoBlob)
    {
        _logger = logger;
        _impuestosExtractor = impuestosExtractor;
        _mefpExtractor = mefpExtractor;
        _goDecreto = gODecreto;
        _goLeyes = gOLeyes;
        _bcbCE = bCBCircularesExternas;
        _bcbR = bCBResoluciones;
        _almacenamiento = almacenamientoBlob;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== Iniciando ciclo único de extracción unificada ===");

        // Ejecución secuencial sumamente limpia utilizando polimorfismo genérico

        await ProcesarExtractorAsync(_impuestosExtractor);
        await ProcesarExtractorAsync(_mefpExtractor);
        await ProcesarExtractorAsync(_goLeyes);
        await ProcesarExtractorAsync(_goDecreto);
        await ProcesarExtractorAsync(_bcbCE);
        await ProcesarExtractorAsync(_bcbR);

        _logger.LogInformation("=== Fin del ciclo único de extracción. Terminando contenedor de forma limpia. ===");
        Environment.Exit(0);
    }

    /// <summary>
    /// MOTOR DE ORQUESTACIÓN GENÉRICO: Ejecuta el flujo completo de persistencia y raspado para cualquier extractor.
    /// </summary>
    private async Task ProcesarExtractorAsync<T>(IExtractorWeb<T> extractor)
    {
        try
        {
            _logger.LogInformation("Iniciando proceso para: [{Sitio}]", extractor.NombreSitio);
            var modeloVacio = new ResultadosModel<T>();

            var estadoActual = await _almacenamiento.ObtenerUltimoEstadoAsync<T>(extractor.NombreSitio, "");

            long criterio = GestorEstadoRpa.ObtenerFechaCriterio(estadoActual, extractor);

            if (estadoActual != null)
            {
                if (!estadoActual.Procesado)
                {
                    criterio = new DateTime(criterio).Ticks - 864000000000;
                }
            }

            var idsExcluidos = new List<string>();
            if (estadoActual?.ContenidoIdentificado?.Any() == true)
            {
                var registroMaximo = estadoActual.ContenidoIdentificado
                    .FirstOrDefault(x => modeloVacio.convertirHora(extractor.SeleccionarFechaString(x)) == criterio);
                if (estadoActual?.ContenidoIdentificado?.Any() == true)
                {
                    idsExcluidos = estadoActual.ContenidoIdentificado
                    .Where(x => modeloVacio.convertirHora(extractor.SeleccionarFechaString(x)) == criterio)
                    .Select(x => extractor.SeleccionarIdentificadorUnico(x))
                    .ToList();
                }
            }

            var resultadosNuevos = await extractor.ExtraerDatosAsync(criterio, idsExcluidos);

            if (estadoActual != null)
            {
                if (resultadosNuevos?.ContenidoIdentificado?.Count > 0)
                {
                    _logger.LogInformation("¡Novedades detectadas ({Count})! almacenando",
                        resultadosNuevos.ContenidoIdentificado.Count, extractor.NombreSitio);
                    await _almacenamiento.EliminarEstadoPorFechaAsync<T>(extractor.NombreSitio, estadoActual.FechaVerificacion);
                    await _almacenamiento.GuardarEstadoAsync(resultadosNuevos, extractor.NombreSitio, $"doc_{DateTime.UtcNow.Ticks}.json");
                }
                else
                {
                    _logger.LogInformation("No se encontraron nuevos registros");
                }
            }
            else
            {
                _logger.LogInformation("¡Novedades detectadas ({Count})! almacenando",
                resultadosNuevos.ContenidoIdentificado.Count, extractor.NombreSitio);
                await _almacenamiento.GuardarEstadoAsync(resultadosNuevos, extractor.NombreSitio, $"doc_{DateTime.UtcNow.Ticks}.json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la ejecución del extractor: {Sitio}", extractor.NombreSitio);
        }
    }
}