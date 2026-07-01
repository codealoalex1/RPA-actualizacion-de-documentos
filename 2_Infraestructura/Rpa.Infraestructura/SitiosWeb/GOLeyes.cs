using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class GOLeyes : IExtractorWeb<GOLeyModel>
    {
        public string NombreSitio => "GACETA OFICIAL del Estado Plurinacional de Bolivia | Listado de leyes";

        // Antes estaba sin http://
        // Playwright necesita una URL completa.
        public string UrlSitioWeb => "http://www.gacetaoficialdebolivia.gob.bo/normas/listadonor/10";

        private static string? ConvertirUrlAbsoluta(string? href)
        {
            if (string.IsNullOrWhiteSpace(href)) return href;

            if (Uri.TryCreate(href, UriKind.Absolute, out var absoluta))
                return absoluta.ToString();

            var baseUri = new Uri("http://www.gacetaoficialdebolivia.gob.bo");
            return new Uri(baseUri, href).ToString();
        }

        public async Task<ResultadosModel<GOLeyModel>> ExtraerDatosAsync()
        {
            ResultadosModel<GOLeyModel> resultadosModel = new()
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

            // Evita cargar imágenes, fuentes y multimedia.
            // Esto ayuda a que la página HTTP no se quede bloqueada cargando recursos innecesarios.
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
                        Console.WriteLine($"Intentando abrir Gaceta Leyes por HTTP: {UrlSitioWeb}. Intento {intento}/{maxReintentos}");

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

                        Console.WriteLine($"Fallo intento {intento}/{maxReintentos} en Gaceta Leyes: {ex.Message}");

                        if (intento == maxReintentos)
                            throw new Exception($"No se pudo abrir {NombreSitio} por HTTP después de {maxReintentos} intentos.", ultimoError);

                        await Task.Delay(intento * 3000);
                    }
                }

                await pagina.Locator("#titulos-bloque .row").First.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = 20000
                });

                var leyes = await pagina.Locator("#titulos-bloque .row").AllAsync();

                foreach (var ley in leyes)
                {
                    GOLeyModel gOLeyModel = new();

                    var cuerpo = ley.Locator("div .card .card-body");

                    if (await cuerpo.CountAsync() == 0)
                        continue;

                    var textoMeta = await cuerpo.Locator(".card-text.texto-default").InnerTextAsync();

                    var partes = textoMeta.Split(" | Fecha de Publicación: ");

                    if (partes.Length < 2)
                        continue;

                    string fecha = partes[1].Split("|")[0].Trim();

                    if (resultadosModel.convertirHora(fecha) < resultadosModel.convertirHora("2026-05-29"))
                        continue;

                    var partesEdicion = partes[0].Split(": ");

                    string edicion = partesEdicion.Length > 1
                        ? partesEdicion[1].Trim()
                        : partes[0].Trim();

                    gOLeyModel.Edicion = edicion;
                    gOLeyModel.FechaPublicacion = fecha;
                    gOLeyModel.Titulo = await cuerpo.Locator("h6 b").InnerTextAsync();
                    gOLeyModel.Descripcion = await cuerpo.Locator(".contentpaneopen p").InnerTextAsync();

                    var enlaces = await ley.Locator("div .card .card-footer a").AllAsync();

                    if (enlaces.Count >= 3)
                    {
                        gOLeyModel.UrlVisual = ConvertirUrlAbsoluta(await enlaces[0].GetAttributeAsync("href"));
                        gOLeyModel.UrlWord = ConvertirUrlAbsoluta(await enlaces[1].GetAttributeAsync("href"));
                        gOLeyModel.UrlPdf = ConvertirUrlAbsoluta(await enlaces[2].GetAttributeAsync("href"));
                    }

                    resultadosModel.ContenidoIdentificado.Add(gOLeyModel);
                }

                Console.WriteLine($"Gaceta Leyes procesada. Registros encontrados: {resultadosModel.ContenidoIdentificado.Count}");
            }
            catch (Exception ex)
            {
                resultadosModel.Status = $"Error: {ex.Message}";
                Console.WriteLine(ex);
            }

            return resultadosModel;
        }
    }
}