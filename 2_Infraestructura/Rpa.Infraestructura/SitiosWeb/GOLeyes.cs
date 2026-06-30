using System.Text.Json;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class GOLeyes : IExtractorWeb<GOLeyModel>
    {
        public string NombreSitio => "GACETA OFICIAL del Estado Plurinacional de Bolivia | Listado de leyes ";
        public string UrlSitioWeb => "www.gacetaoficialdebolivia.gob.bo/normas/listadonor/10";

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
                            Timeout = 50000,
                            WaitUntil = WaitUntilState.Load
                        });

                        exitoNavegacion = true;
                    }
                    catch (TimeoutException ex)
                    {
                        Console.WriteLine($"TIMEOUT: Intento {intento}/{maxReintentos} falló en {NombreSitio}. Detalle: {ex.Message}");

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

                var leyes = await pagina.Locator("#titulos-bloque .row").AllAsync();

                if (leyes.Count > 0)
                {
                    foreach (var decreto in leyes)
                    {
                        GOLeyModel GOLeyModel = new();
                        var cuerpo = decreto.Locator("div .card .card-body");

                        if (await cuerpo.CountAsync() == 0) continue;

                        var cuerpoTexto = (await cuerpo.Locator(".card-text.texto-default").InnerTextAsync()).Split(" | Fecha de Publicación: ");
                        string fecha = cuerpoTexto[1].Split("|")[0];

                        if (resultadosModel.convertirHora(fecha) < resultadosModel.convertirHora("2026-05-29")) continue;

                        string edicion = cuerpoTexto[0].Split(": ")[1];

                        GOLeyModel.Edicion = edicion;
                        GOLeyModel.FechaPublicacion = fecha;

                        GOLeyModel.Titulo = await cuerpo.Locator("h6 b").InnerTextAsync();
                        GOLeyModel.Descripcion = await cuerpo.Locator(".contentpaneopen p").InnerTextAsync();

                        var enlaces = await decreto.Locator("div .card .card-footer a").AllAsync();
                        GOLeyModel.UrlVisual = await enlaces[0].GetAttributeAsync("href");
                        GOLeyModel.UrlWord = await enlaces[1].GetAttributeAsync("href");
                        GOLeyModel.UrlPdf = await enlaces[2].GetAttributeAsync("href");

                        resultadosModel.ContenidoIdentificado.Add(GOLeyModel);
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