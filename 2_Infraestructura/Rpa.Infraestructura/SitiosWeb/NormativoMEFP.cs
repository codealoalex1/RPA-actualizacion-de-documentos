using System.Text.Json;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb
{
    public class NormativaMEFP : IExtractorWeb<MEFPModel>
    {
        public string NombreSitio => "Normativa Ministerio de Economía y Finanzas Públicas";
        public string UrlSitioWeb => "https://www.economiayfinanzas.gob.bo/index.php/normativa?field_tipo_de_normativa_target_id=All&field_viceministerio_target_id=All&title=&body_value=&field_gestion_value=";
        public async Task<ResultadosModel<MEFPModel>> ExtraerDatosAsync(long dateTime)
        {
            ResultadosModel<MEFPModel> resultadosModel = new()
            {
                SitioWeb = NombreSitio,
                Status = "Procesado exitosamente"
            };

            using var playwright = await Playwright.CreateAsync();
            await using var navegador = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });


            // Configurar el contexto con un User-Agent real para evitar bloqueos automatizados
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

                var tabla = await pagina.Locator(".table.table-striped.table-hover.table-condensed.cols-10.sticky-enabled tbody tr").AllAsync();

                if (tabla.Count > 0)
                {
                    foreach (var fila in tabla)
                    {
                        MEFPModel mEFPModel = new();
                        var columnas = await fila.Locator("td").AllAsync();
                        string fecha = await columnas[9].InnerTextAsync();
                        if (await columnas[5].InnerTextAsync() != "2026" || resultadosModel.convertirHora(fecha) <= dateTime)
                        {
                            continue;
                        }
                        mEFPModel.Titulo = await columnas[0].InnerTextAsync();
                        mEFPModel.Descripcion = await columnas[1].InnerTextAsync();
                        mEFPModel.TipoNormativa = await columnas[2].InnerTextAsync();
                        mEFPModel.Area = await columnas[3].InnerTextAsync();
                        mEFPModel.Periodo = await columnas[4].InnerTextAsync();
                        mEFPModel.Gestion = int.Parse(await columnas[5].InnerTextAsync());
                        var urls = await columnas[6].Locator("a").AllAsync();
                        if (urls.Count > 0)
                        {
                            foreach (var url in urls)
                            {
                                mEFPModel.UrlPdf.Add(await url.GetAttributeAsync("href") ?? "");
                            }
                        }
                        var urlsAnexos = await columnas[7].Locator("a").AllAsync();
                        if (urlsAnexos.Count > 0)
                        {
                            foreach (var url in urlsAnexos)
                            {
                                mEFPModel.Anexos.Add(await url.GetAttributeAsync("href") ?? "");
                            }
                        }
                        mEFPModel.FechaPublicacion = fecha;
                        resultadosModel.ContenidoIdentificado.Add(mEFPModel);
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