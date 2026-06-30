using System.Threading.Tasks;
using Rpa.Nucleo.Modelos;

namespace Rpa.Nucleo.Interfaces;

public interface IAlmacenamientoServicio
{
    // Descarga el último JSON del Storage para un sitio específico
    Task<ResultadosModel<T>?> ObtenerUltimoEstadoAsync<T>(string nombreSitio, string nombreArchivo);

    // Sube el nuevo JSON generado al contenedor de Azure
    Task GuardarEstadoAsync<T>(ResultadosModel<T> datos, string nombreSitio, string nombreArchivo);
}