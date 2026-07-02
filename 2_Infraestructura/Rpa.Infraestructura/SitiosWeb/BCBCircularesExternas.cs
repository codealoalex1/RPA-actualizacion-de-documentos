using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class BCBCircularesExternas : IExtractorWeb<BCBCircularesExternasModel>
{
    public string NombreSitio => "BCB Circulares Externas";
    public string UrlSitioWeb => "https://www.bcb.gob.bo/?q=circulares-externas";

    public string SeleccionarFechaString(BCBCircularesExternasModel modelo) => modelo.Fecha ?? string.Empty;
    public string SeleccionarIdentificadorUnico(BCBCircularesExternasModel modelo) => modelo.Titulo ?? string.Empty;

    public async Task<ResultadosModel<BCBCircularesExternasModel>> ExtraerDatosAsync(long criterion, IEnumerable<string> id)
    {
        ResultadosModel<BCBCircularesExternasModel> resultadosModel = new()
        {
            SitioWeb = NombreSitio,
            Status = "Procesado exitosamente"
        };

        using var playwright = await Playwright.CreateAsync();
        await using var navegador = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        var pagina = await contexto.NewPageAsync();

        int maxReintentos = 3;
        for (int intento = 1; intento <= maxReintentos; intento++)
        {
            try
            {
                await pagina.GotoAsync(UrlSitioWeb, new PageGotoOptions { Timeout = 45000, WaitUntil = WaitUntilState.DOMContentLoaded });
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REINTENTO {intento}/{maxReintentos}] Error en BCB Circulares: {ex.Message}");
                if (intento == maxReintentos) throw;
                await Task.Delay(2000 * intento);
            }
        }

        try
        {
            var selectorDesplegable = pagina.Locator("#edit-field-gestion-ce-value-value-year");
            await selectorDesplegable.SelectOptionAsync(new[] { "2026" });
            var boton = pagina.Locator("#edit-submit-circulares-externas");
            await boton.ClickAsync();
            var resolucionesContent = pagina.Locator(".view-content");
            await resolucionesContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var resoluciones = await resolucionesContent.Locator(".views-row").AllAsync();
            if (resoluciones.Count > 0)
            {
                foreach (var resolucion in resoluciones)
                {
                    string rawDate = await resolucion.Locator("div .bcb_date span").InnerTextAsync();
                    string[] fechaCompleta = rawDate.Split("- ");
                    string[] fechaLiteral = fechaCompleta[0].Split(", ");
                    string titulo = (await resolucion.Locator("div .bcb_title").InnerTextAsync()).Trim();
                    string fecha = (fechaLiteral[1] + " " + fechaLiteral[2] + fechaCompleta[1]).Trim();

                    if (resultadosModel.convertirHora(fecha) < criterion || id.Contains(titulo)) continue;

                    var bCBCircularesExternasModel = new BCBCircularesExternasModel
                    {
                        Fecha = fecha,
                        Titulo = titulo,
                        Contenido = await resolucion.Locator("div .bcb_content").InnerTextAsync(),
                        UrlPdf = await resolucion.Locator("div .bcb_adjunto strong a").GetAttributeAsync("href")
                    };

                    resultadosModel.ContenidoIdentificado.Add(bCBCircularesExternasModel);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error de estructura en BCB Circulares: {ex.Message}");
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultadosModel;
    }
}