namespace Rpa.Nucleo.Modelos
{
    public class MEFPModel
    {
        public string? Titulo { get; set; }
        public string? Descripcion { get; set; }
        public string? TipoNormativa { get; set; }
        public string? Area { get; set; }
        public string? Periodo { get; set; }
        public int Gestion { get; set; }
        public List<string> UrlPdf { get; set; } = new();
        public List<string> Anexos { get; set; } = new();
        public string? FechaPublicacion { get; set; }

        public MEFPModel() { }
    }
}