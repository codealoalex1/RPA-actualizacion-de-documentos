namespace Rpa.Nucleo.Modelos
{
    public class ASFICircularValoresModel
    {
        public string? Circular { get; set; }
        public string? Resolucion { get; set; }
        public string? Fecha { get; set; }
        public string? Referencia { get; set; }
        public string? ArchivoPdf { get; set; }
        public string? UrlPdf { get; set; }
        public string? UrlIndicePdf { get; set; }
        public int PaginaIndice { get; set; }

        public ASFICircularValoresModel() { }
    }
}