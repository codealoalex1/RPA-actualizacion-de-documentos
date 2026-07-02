using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class NormativaMEFP : IExtractorWeb<MEFPModel>
{
    public string NombreSitio => "Normativa Ministerio de Economía y Finanzas Públicas";
    public string UrlSitioWeb => "https://www.economiayfinanzas.gob.bo/index.php/normativa?field_tipo_de_normativa_target_id=All&field_viceministerio_target_id=All&title=&body_value=&field_gestion_value=";

    public string SeleccionarFechaString(MEFPModel modelo) => modelo.FechaPublicacion ?? string.Empty;
    public string SeleccionarIdentificadorUnico(MEFPModel modelo) => modelo.Titulo ?? string.Empty;

    public async Task<ResultadosModel<MEFPModel>> ExtraerDatosAsync(long criterion, string id)
    {
        ResultadosModel<MEFPModel> resultadosModel = new()
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
                Console.WriteLine($"[REINTENTO {intento}/{maxReintentos}] Error en MEFP: {ex.Message}");
                if (intento == maxReintentos) throw;
                await Task.Delay(2000 * intento);
            }
        }

        try
        {
            var tabla = await pagina.Locator(".table.table-striped.table-hover.table-condensed.cols-10.sticky-enabled tbody tr").AllAsync();

            if (tabla.Count > 0)
            {
                foreach (var fila in tabla)
                {
                    var columnas = await fila.Locator("td").AllAsync();
                    if (columnas.Count >= 9)
                    {
                        string fecha = (await columnas[8].InnerTextAsync()).Trim();
                        string titulo = (await columnas[1].InnerTextAsync()).Trim();

                        if (resultadosModel.convertirHora(fecha) < criterion || titulo == id) continue;

                        MEFPModel mEFPModel = new()
                        {
                            Titulo = titulo,
                            Descripcion = await columnas[0].InnerTextAsync(),
                            TipoNormativa = await columnas[2].InnerTextAsync(),
                            Area = await columnas[3].InnerTextAsync(),
                            Periodo = await columnas[4].InnerTextAsync(),
                            Gestion = int.TryParse(await columnas[5].InnerTextAsync(), out int g) ? g : 0,
                            FechaPublicacion = fecha
                        };

                        var urls = await columnas[6].Locator("a").AllAsync();
                        foreach (var url in urls)
                        {
                            mEFPModel.UrlPdf.Add(await url.GetAttributeAsync("href") ?? "");
                        }

                        var urlsAnexos = await columnas[7].Locator("a").AllAsync();
                        foreach (var url in urlsAnexos)
                        {
                            mEFPModel.Anexos.Add(await url.GetAttributeAsync("href") ?? "");
                        }

                        resultadosModel.ContenidoIdentificado.Add(mEFPModel);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parseando datos en MEFP: {ex.Message}");
            resultadosModel.Status = "Error en procesamiento";
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultadosModel;
    }
}