using System.Text.RegularExpressions;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;
using UglyToad.PdfPig;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class ASFICircularValoresExtractor : IExtractorWeb<ASFICircularValoresModel>
    {
        public string NombreSitio => "ASFI | Actualizaciones RNMV Mercado de Valores";
        public string UrlSitioWeb => "https://servdmzw.asfi.gob.bo/CircularValores/Circulares/CircularesMV.pdf";

        private const string BaseCirculares = "https://servdmzw.asfi.gob.bo/CircularValores/Circulares/";

        private static readonly Regex CircularRegex = new(
            @"ASFI\s*-\s*(\d{3})\s+(\d+)\s+(\d{2}/\d{2}/\d{4})\s+(.*?)(?=ASFI\s*-\s*\d{3}|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
        );

        public async Task<ResultadosModel<ASFICircularValoresModel>> ExtraerDatosAsync()
        {
            ResultadosModel<ASFICircularValoresModel> resultadosModel = new()
            {
                SitioWeb = NombreSitio,
                Status = "Procesado exitosamente"
            };

            try
            {
                Console.WriteLine($"Descargando PDF índice ASFI: {UrlSitioWeb}");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(90);

                byte[] pdfBytes = await httpClient.GetByteArrayAsync(UrlSitioWeb);

                using var memoria = new MemoryStream(pdfBytes);
                using var documento = PdfDocument.Open(memoria);

                int numeroPagina = 0;

                foreach (var pagina in documento.GetPages())
                {
                    numeroPagina++;

                    string textoPagina = string.Join(" ", pagina.GetWords().Select(p => p.Text));

                    if (!textoPagina.Contains("GESTIÓN 2026") && !textoPagina.Contains("GESTION 2026"))
                        continue;

                    foreach (Match match in CircularRegex.Matches(textoPagina))
                    {
                        string numeroCircular = match.Groups[1].Value.Trim();
                        string resolucion = match.Groups[2].Value.Trim();
                        string fecha = match.Groups[3].Value.Trim();
                        string referencia = LimpiarReferencia(match.Groups[4].Value);

                        string archivo = $"ASFI_{numeroCircular}.pdf";
                        string urlPdf = new Uri(new Uri(BaseCirculares), archivo).ToString();

                        ASFICircularValoresModel modelo = new()
                        {
                            Circular = $"ASFI - {numeroCircular}",
                            Resolucion = resolucion,
                            Fecha = fecha,
                            Referencia = referencia,
                            ArchivoPdf = archivo,
                            UrlPdf = urlPdf,
                            UrlIndicePdf = UrlSitioWeb,
                            PaginaIndice = numeroPagina
                        };

                        resultadosModel.ContenidoIdentificado.Add(modelo);
                    }
                }

                Console.WriteLine($"ASFI RNMV procesado. PDFs encontrados: {resultadosModel.ContenidoIdentificado.Count}");
            }
            catch (Exception ex)
            {
                resultadosModel.Status = "Error en la Extracción del Sitio";
                Console.WriteLine(ex);
            }

            return resultadosModel;
        }

        private static string LimpiarReferencia(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            texto = Regex.Replace(texto, @"\s+", " ").Trim();

            int indiceRnmv = texto.IndexOf("RNMV", StringComparison.OrdinalIgnoreCase);

            if (indiceRnmv >= 0)
                texto = texto[..indiceRnmv].Trim();

            return texto;
        }
        public string SeleccionarFechaString(ASFICircularValoresModel item)
        {
            return item.Fecha ?? string.Empty;
        }

        public string SeleccionarIdentificadorUnico(ASFICircularValoresModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.UrlPdf))
                return item.UrlPdf;

            if (!string.IsNullOrWhiteSpace(item.ArchivoPdf))
                return item.ArchivoPdf;

            return item.Circular ?? string.Empty;
        }

        public async Task<ResultadosModel<ASFICircularValoresModel>> ExtraerDatosAsync(
            long criterion,
            IEnumerable<string> idsExcluidos)
        {
            var resultado = await ExtraerDatosAsync();

            var ids = new HashSet<string>(
                idsExcluidos ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase
            );

            resultado.ContenidoIdentificado = resultado.ContenidoIdentificado
                .Where(item =>
                {
                    long fechaItem = resultado.convertirHora(SeleccionarFechaString(item));
                    string idItem = SeleccionarIdentificadorUnico(item);

                    bool esNuevoPorFecha = fechaItem > criterion;
                    bool mismaFechaPeroNoExiste = fechaItem == criterion && !ids.Contains(idItem);

                    return esNuevoPorFecha || mismaFechaPeroNoExiste;
                })
                .ToList();

            return resultado;
        }
    }
}