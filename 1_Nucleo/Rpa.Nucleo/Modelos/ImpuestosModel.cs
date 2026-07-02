namespace Rpa.Nucleo.Modelos;

public class ImpuestosModel
{
    public string? Titulo { get; set; }
    public string? Fecha { get; set; }
    public string? Id { get; set; }
    public string? UrlPdf { get; set; }

    // Constructor vacío obligatorio para la deserialización de System.Text.Json
    public ImpuestosModel()
    {
    }

    // Constructor útil para cuando el scraper cree instancias nuevas
}