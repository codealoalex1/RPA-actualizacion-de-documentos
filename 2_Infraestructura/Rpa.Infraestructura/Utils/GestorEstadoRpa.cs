using System;
using System.Linq;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.Utilidades
{
    public static class GestorEstadoRpa
    {
        /// <summary>
        /// Evalúa el objeto recuperado de Azure Blob Storage para extraer la fecha más reciente de forma dinámica.
        /// </summary>
        public static long ObtenerFechaCriterio<T>(
            ResultadosModel<T>? estadoAzure, 
            Func<T, string> selectorFecha, 
            Func<string, long> funcionConvertirHora)
        {
            // Si el archivo no existía en Azure Blob Storage (primera corrida del robot)
            if (estadoAzure == null || estadoAzure.ContenidoIdentificado?.Any() != true)
            {
                return DateTime.MinValue.Ticks; 
            }

            try
            {
                // Obtenemos la fecha máxima iterando dinámicamente sobre "ContenidoIdentificado"
                return estadoAzure.ContenidoIdentificado
                    .Select(x => funcionConvertirHora(selectorFecha(x)))
                    .Max();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GestorEstado] Error al calcular la fecha criterio (se usará fecha mínima): {ex.Message}");
                return DateTime.MinValue.Ticks;
            }
        }
    }
}