using System.Text.Json;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class GODecreto : IExtractorWeb<GODecretoModel>
    {
        public string NombreSitio => "GACETA OFICIAL del Estado Plurinacional de Bolivia | Listado de decretos ";
        public string UrlSitioWeb => "http://www.gacetaoficialdebolivia.gob.bo/normas/listadonor/11";
        public async Task<ResultadosModel<GODecretoModel>> ExtraerDatosAsync(long dateTime, string id)
        {
            ResultadosModel<GODecretoModel> resultadosModel = new()
            {
                SitioWeb = NombreSitio,
                Status = "Procesado exitosamente"
            };

            using var playwright = await Playwright.CreateAsync();

            await using var navegador = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[]
                {
                    "--allow-running-insecure-content"
                }
            });

            var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "es-BO",
                ServiceWorkers = ServiceWorkerPolicy.Block,
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "es-BO,es;q=0.9,en;q=0.8"
                }
            });

            await contexto.RouteAsync("**/*", async ruta =>
                        {
                            string tipo = ruta.Request.ResourceType;

                            if (tipo == "image" || tipo == "font" || tipo == "media")
                                await ruta.AbortAsync();
                            else
                                await ruta.ContinueAsync();
                        });

            var pagina = await contexto.NewPageAsync();

            pagina.SetDefaultTimeout(30000);
            pagina.SetDefaultNavigationTimeout(60000);

            try
            {
                int maxReintentos = 3;
                Exception? ultimoError = null;
                IResponse? respuesta = null;

                for (int intento = 1; intento <= maxReintentos; intento++)
                {
                    try
                    {
                        Console.WriteLine($"Intentando abrir Gaceta Decretos por HTTP: {UrlSitioWeb}. Intento {intento}/{maxReintentos}");

                        respuesta = await pagina.GotoAsync(UrlSitioWeb, new PageGotoOptions
                        {
                            Timeout = 60000,
                            WaitUntil = WaitUntilState.DOMContentLoaded
                        });

                        if (respuesta == null || respuesta.Status < 400)
                            break;

                        throw new Exception($"La página respondió con estado HTTP {respuesta.Status}");
                    }
                    catch (Exception ex)
                    {
                        ultimoError = ex;

                        Console.WriteLine($"Fallo intento {intento}/{maxReintentos} en Gaceta Decretos: {ex.Message}");

                        if (intento == maxReintentos)
                            throw new Exception($"No se pudo abrir {NombreSitio} por HTTP después de {maxReintentos} intentos.", ultimoError);

                        await Task.Delay(intento * 3000);
                    }
                }

                await pagina.Locator("#titulos-bloque .row").First.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = 20000
                });

                var decretos = await pagina.Locator("#titulos-bloque .row").AllAsync();

                if (decretos.Count > 0)
                {
                    foreach (var decreto in decretos)
                    {
                        GODecretoModel gODecretoModel = new();
                        var cuerpo = decreto.Locator("div .card .card-body");

                        if (await cuerpo.CountAsync() == 0) continue;

                        var cuerpoTexto = (await cuerpo.Locator(".card-text.texto-default").InnerTextAsync()).Split(" | Fecha de Publicación: ");
                        string fecha = cuerpoTexto[1].Split("|")[0];
                        string titulo = await cuerpo.Locator("h6 b").InnerTextAsync();
                        if (resultadosModel.convertirHora(fecha) < dateTime || titulo == id) continue;

                        string edicion = cuerpoTexto[0].Split(": ")[1];

                        gODecretoModel.Edicion = edicion;
                        gODecretoModel.FechaPublicacion = fecha;

                        gODecretoModel.Titulo = titulo;
                        gODecretoModel.Descripcion = await cuerpo.Locator(".contentpaneopen p").InnerTextAsync();

                        var enlaces = await decreto.Locator("div .card .card-footer a").AllAsync();
                        gODecretoModel.UrlVisual = await enlaces[0].GetAttributeAsync("href");
                        gODecretoModel.UrlWord = await enlaces[1].GetAttributeAsync("href");
                        gODecretoModel.UrlPdf = await enlaces[2].GetAttributeAsync("href");

                        resultadosModel.ContenidoIdentificado.Add(gODecretoModel);
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