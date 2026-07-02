using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class GODecreto : IExtractorWeb<GODecretoModel>
{
    public string NombreSitio => "GACETA OFICIAL del Estado Plurinacional de Bolivia | Listado de decretos ";
    public string UrlSitioWeb => "http://www.gacetaoficialdebolivia.gob.bo/normas/listadonor/11";

    public string SeleccionarFechaString(GODecretoModel modelo) => modelo.FechaPublicacion ?? string.Empty;
    public string SeleccionarIdentificadorUnico(GODecretoModel modelo) => modelo.Titulo ?? string.Empty;

    public async Task<ResultadosModel<GODecretoModel>> ExtraerDatosAsync(long criterion, string id)
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
            Args = new[] { "--allow-running-insecure-content" }
        });

        var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true // Forzado técnico para evadir problemas de certificados SSL estatales
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
                Console.WriteLine($"[REINTENTO {intento}/{maxReintentos}] Error en Gaceta Decretos: {ex.Message}");
                if (intento == maxReintentos) throw;
                await Task.Delay(2000 * intento);
            }
        }

        try
        {
            await pagina.Locator("#titulos-bloque .row").First.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = 20000
            });

            var decretos = await pagina.Locator("#titulos-bloque .row").AllAsync();

            foreach (var decreto in decretos)
            {
                var cuerpo = decreto.Locator("div .card .card-body");
                if (await cuerpo.CountAsync() == 0) continue;

                var cuerpoTexto = (await cuerpo.Locator(".card-text.texto-default").InnerTextAsync()).Split(" | Fecha de Publicación: ");
                if (cuerpoTexto.Length < 2) continue;

                string fecha = cuerpoTexto[1].Split("|")[0].Trim();
                string titulo = (await cuerpo.Locator("h6 b").InnerTextAsync()).Trim();

                if (resultadosModel.convertirHora(fecha) < criterion || titulo == id) continue;

                string edicion = cuerpoTexto[0].Split(": ")[1];

                var gODecretoModel = new GODecretoModel
                {
                    Edicion = edicion,
                    FechaPublicacion = fecha,
                    Titulo = titulo,
                    Descripcion = await cuerpo.Locator(".contentpaneopen p").InnerTextAsync()
                };

                var enlaces = await decreto.Locator("div .card .card-footer a").AllAsync();
                if (enlaces.Count >= 3)
                {
                    gODecretoModel.UrlVisual = await enlaces[0].GetAttributeAsync("href");
                    gODecretoModel.UrlWord = await enlaces[1].GetAttributeAsync("href");
                    gODecretoModel.UrlPdf = await enlaces[2].GetAttributeAsync("href");
                }

                resultadosModel.ContenidoIdentificado.Add(gODecretoModel);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parseando Gaceta Decretos: {ex.Message}");
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultadosModel;
    }
}