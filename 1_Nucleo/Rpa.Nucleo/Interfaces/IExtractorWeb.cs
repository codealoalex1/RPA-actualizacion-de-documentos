using System.Threading.Tasks;
using Rpa.Nucleo.Modelos;

namespace Rpa.Nucleo.Interfaces;

public interface IExtractorWeb<T>
{
    // Identificador único del sitio (ej: "Impuestos_RND_2026")
    string NombreSitio { get; }
    string UrlSitioWeb { get; }

    // NUEVO: Permite al orquestador delegar la extracción de la fecha y el identificador de un registro al extractor
    string SeleccionarFechaString(T modelo);
    string SeleccionarIdentificadorUnico(T modelo);

    // Método principal que ejecutará Playwright internamente para extraer los datos
    Task<ResultadosModel<T>> ExtraerDatosAsync (long criterio, IEnumerable<string> idsExcluidos);
}