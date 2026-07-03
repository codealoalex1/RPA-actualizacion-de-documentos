using System;
using System.Linq;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.Utilidades;

public static class GestorEstadoRpa
{
    /// <summary>
    /// Evalúa el objeto recuperado de Azure Blob Storage para extraer la fecha más reciente de forma dinámica.
    /// </summary>
    public static long ObtenerFechaCriterio<T>(ResultadosModel<T>? estadoAzure, IExtractorWeb<T> extractor)
    {
        if (estadoAzure == null || estadoAzure.ContenidoIdentificado?.Any() != true)
        {
            return DateTime.MinValue.Ticks;
        }

        try
        {
            if (estadoAzure.Procesado)
            {
                return estadoAzure.ContenidoIdentificado
                .Select(x => estadoAzure.convertirHora(extractor.SeleccionarFechaString(x)))
                .Max();
            }
            else
            {
                return estadoAzure.ContenidoIdentificado
                .Select(x => estadoAzure.convertirHora(extractor.SeleccionarFechaString(x)))
                .Min();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GestorEstado] Error al calcular la fecha criterio para '{extractor.NombreSitio}': {ex.Message}");
            return DateTime.MinValue.Ticks;
        }
    }
}