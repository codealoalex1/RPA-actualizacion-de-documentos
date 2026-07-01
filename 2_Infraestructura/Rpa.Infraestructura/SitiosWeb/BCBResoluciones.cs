using System.Text.Json;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;
using System.Globalization;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class BCBResoluciones : IExtractorWeb<BCBResolucionesModel>
    {
        public string NombreSitio => "BCB Resoluciones de directorio";
        public string UrlSitioWeb => "https://www.bcb.gob.bo/?q=resoluciones-de-directorio";
        public async Task<ResultadosModel<BCBResolucionesModel>> ExtraerDatosAsync(long dateTime, string id)
        {
            ResultadosModel<BCBResolucionesModel> resultadosModel = new()
            {
                SitioWeb = NombreSitio,
                Status = "Procesado exitosamente"
            };

            using var playwright = await Playwright.CreateAsync();
            await using var navegador = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            var pagina = await contexto.NewPageAsync();

            try
            {
                int maxReintentos = 3;
                int intento = 0;
                bool exitoNavegacion = false;

                while (intento < maxReintentos && !exitoNavegacion)
                {
                    try
                    {
                        intento++;

                        await pagina.GotoAsync(UrlSitioWeb, new PageGotoOptions
                        {
                            Timeout = 45000,
                            WaitUntil = WaitUntilState.Load
                        });

                        exitoNavegacion = true;
                    }
                    catch (TimeoutException ex)
                    {
                        Console.WriteLine($"[TIMEOUT] Intento {intento}/{maxReintentos} falló en {NombreSitio}. Detalle: {ex.Message}");

                        if (intento >= maxReintentos)
                        {
                            throw new Exception($"Saturación de red: Imposible conectar a {NombreSitio} tras {maxReintentos} intentos.");
                        }

                        // Tiempo de espera exponencial: Intento 1 = 3s, Intento 2 = 6s
                        int tiempoEspera = intento * 3000;
                        Console.WriteLine($"Esperando {tiempoEspera / 1000} segundos antes de reintentar...");
                        await Task.Delay(tiempoEspera);
                    }
                }

                var selectorDesplegable = pagina.Locator("#edit-field-fecha-resolucion-value-value-year");
                await selectorDesplegable.SelectOptionAsync(new[] { "2026" });
                var boton = pagina.Locator("#edit-submit-resoluciones-de-directorio");
                await boton.ClickAsync();
                var resolucionesContent = pagina.Locator(".view-content");
                await resolucionesContent.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
                var resoluciones = await resolucionesContent.Locator(".views-row").AllAsync();
                if (resoluciones.Count > 0)
                {
                    foreach (var resolucion in resoluciones)
                    {
                        BCBResolucionesModel bCBResolucionesModel = new();
                        var fechas = await resolucion.Locator("div .bcb_date span").AllAsync();
                        string fechaResolucion = await fechas[0].InnerTextAsync();
                        string fechaPublicacion = await fechas[1].InnerTextAsync();
                        string reso = await resolucion.Locator("div .bcb_title a").InnerTextAsync();
                        if (resultadosModel.convertirHora(fechaPublicacion) < dateTime || reso == id) { continue; }
                        bCBResolucionesModel.Resolucion = reso;
                        bCBResolucionesModel.FechaResolucion = fechaResolucion;
                        bCBResolucionesModel.FechaPublicacion = fechaPublicacion;
                        bCBResolucionesModel.Descripcion = await resolucion.Locator("div .bcb_content p").InnerTextAsync();
                        bCBResolucionesModel.UrlPdf = await resolucion.Locator("div .bcb_adjunto strong a").GetAttributeAsync("href");

                        resultadosModel.ContenidoIdentificado.Add(bCBResolucionesModel);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return resultadosModel;
        }
    }
}