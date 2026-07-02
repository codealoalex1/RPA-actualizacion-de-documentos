using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class BCBResoluciones : IExtractorWeb<BCBResolucionesModel>
{
    public string NombreSitio => "BCB Resoluciones de directorio";
    public string UrlSitioWeb => "https://www.bcb.gob.bo/?q=resoluciones-de-directorio";

    public string SeleccionarFechaString(BCBResolucionesModel modelo) => modelo.FechaPublicacion ?? string.Empty;
    public string SeleccionarIdentificadorUnico(BCBResolucionesModel modelo) => modelo.Resolucion ?? string.Empty;

    public async Task<ResultadosModel<BCBResolucionesModel>> ExtraerDatosAsync(long criterion, string id)
    {
        ResultadosModel<BCBResolucionesModel> resultadosModel = new()
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
                Console.WriteLine($"[REINTENTO {intento}/{maxReintentos}] Error en BCB Resoluciones: {ex.Message}");
                if (intento == maxReintentos) throw;
                await Task.Delay(2000 * intento);
            }
        }

        try
        {
            var selectorDesplegable = pagina.Locator("#edit-field-fecha-resolucion-value-value-year");
            await selectorDesplegable.SelectOptionAsync(new[] { "2026" });
            var boton = pagina.Locator("#edit-submit-resoluciones-de-directorio");
            await boton.ClickAsync();
            var resolucionesContent = pagina.Locator(".view-content");
            await resolucionesContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            var resoluciones = await resolucionesContent.Locator(".views-row").AllAsync();

            foreach (var resolucion in resoluciones)
            {
                var fechas = await resolucion.Locator("div .bcb_date span").AllAsync();
                if (fechas.Count < 2) continue;

                string fechaResolucion = (await fechas[0].InnerTextAsync()).Trim();
                string fechaPublicacion = (await fechas[1].InnerTextAsync()).Trim();
                string reso = (await resolucion.Locator("div .bcb_title a").InnerTextAsync()).Trim();

                if (resultadosModel.convertirHora(fechaPublicacion) < criterion || reso == id) continue;

                var bCBResolucionesModel = new BCBResolucionesModel
                {
                    Resolucion = reso,
                    FechaResolucion = fechaResolucion,
                    FechaPublicacion = fechaPublicacion,
                    Descripcion = await resolucion.Locator("div .bcb_content p").InnerTextAsync(),
                    UrlPdf = await resolucion.Locator("div .bcb_adjunto strong a").GetAttributeAsync("href")
                };

                resultadosModel.ContenidoIdentificado.Add(bCBResolucionesModel);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error de parsing en BCB Resoluciones: {ex.Message}");
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultadosModel;
    }
}