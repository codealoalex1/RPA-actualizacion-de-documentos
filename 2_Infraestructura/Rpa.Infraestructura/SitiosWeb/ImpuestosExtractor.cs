using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class ImpuestosExtractor : IExtractorWeb<ImpuestosModel>
{
    public string NombreSitio => "Servicio de Impuestos Nacionales | Entidad facilitadora del cumplimiento de las obligaciones tributarias";
    public string UrlSitioWeb => "https://www.impuestos.gob.bo/index.php/rnd-2026/";

    public string SeleccionarFechaString(ImpuestosModel modelo) => modelo.Fecha ?? string.Empty;
    public string SeleccionarIdentificadorUnico(ImpuestosModel modelo) => modelo.Titulo ?? string.Empty;

    public async Task<ResultadosModel<ImpuestosModel>> ExtraerDatosAsync(long criterion, IEnumerable<string> ids)
    {
        var resultadosModel = new ResultadosModel<ImpuestosModel>
        {
            SitioWeb = NombreSitio,
            Status = "Procesado Exitosamente"
        };

        using var playwright = await Playwright.CreateAsync();
        await using var navegador = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        var contexto = await navegador.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        var pagina = await contexto.NewPageAsync();

        // --- BLINDAJE CON REINTENTOS ASÍNCRONOS ---
        int maxReintentos = 3;
        int delayBaseMilisegundos = 2000;

        for (int intento = 1; intento <= maxReintentos; intento++)
        {
            try
            {
                await pagina.GotoAsync(UrlSitioWeb, new PageGotoOptions { Timeout = 45000, WaitUntil = WaitUntilState.DOMContentLoaded });
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REINTENTO {intento}/{maxReintentos}] Error navegando a Impuestos: {ex.Message}");
                if (intento == maxReintentos) throw;
                await Task.Delay(delayBaseMilisegundos * intento);
            }
        }

        try
        {
            var localizadorFilas = pagina.Locator(".rnd-table table tbody tr");
            await localizadorFilas.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

            var filas = await localizadorFilas.AllAsync();
            var registrosExtraidos = new List<ImpuestosModel>();
            
            foreach (var fila in filas)
            {
                var celdas = await fila.Locator("td").AllAsync();

                if (celdas.Count >= 1)
                {
                    var celdaObjetivo = celdas[0];
                    string fecha = (await celdaObjetivo.Locator(".rnd-dates").InnerTextAsync()).Split("| Fecha de publicación: ")[1];

                    // Condicional para verificar cuales son los documentos más recientes
                    // Cambiar DateTime.Today.Ticks por la fecha a evaluar 
                    if (resultadosModel.convertirHora(fecha) < criterion)
                    {
                        continue;
                    }

                    string textoTitulo = await celdaObjetivo.EvaluateAsync<string>(@"element => {
        const textos = Array.from(element.childNodes)
            .filter(node => node.nodeType === Node.TEXT_NODE)
            .map(node => node.textContent.trim())
            .filter(text => text.length > 0);
            if(textos.length == 1) return textos[0];
            if(textos.length > 1) return textos[0]+textos[1];
            if(textos.length <= 0) return ''; 
    }");

                    textoTitulo = textoTitulo.Replace("\"", "").Trim();
                    if(ids.Contains(textoTitulo)) continue;

                    string textoId = string.Empty;
                    string urlPdf = string.Empty;

                    var strongElement = await celdaObjetivo.Locator("strong").AllAsync();
                    if (strongElement.Count == 0)
                    {
                        textoId = await strongElement[0].InnerTextAsync() ?? string.Empty;
                    }
                    if (strongElement.Count > 0)
                    {
                        foreach (var item in strongElement)
                        {
                            if (await item.InnerTextAsync() == "")
                            {
                                continue;
                            }
                            textoId = await item.InnerTextAsync() ?? string.Empty;
                        }
                    }

                    var linkElement = celdaObjetivo.Locator("a");
                    if (await linkElement.CountAsync() > 0)
                    {
                        urlPdf = (await linkElement.First.GetAttributeAsync("href")) ?? string.Empty;
                    }

                    var nuevoRegistro = new ImpuestosModel()
                    {
                        Titulo = textoTitulo,
                        Id = textoId.Trim(),
                        UrlPdf = urlPdf.Trim(),
                        Fecha = fecha
                    };

                    registrosExtraidos.Add(nuevoRegistro);
                }
            }

            resultadosModel.ContenidoIdentificado = registrosExtraidos;
        }
        catch (Exception ex)
        {
            resultadosModel.ErrorMessage = ex.Message;
            resultadosModel.Status = "Error";
            throw;
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultadosModel;
    }
}