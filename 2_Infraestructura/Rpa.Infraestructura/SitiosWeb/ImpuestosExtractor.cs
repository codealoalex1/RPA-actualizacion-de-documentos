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
    public string SeleccionarIdentificadorUnico(ImpuestosModel modelo) => modelo.Id ?? string.Empty;

    public async Task<ResultadosModel<ImpuestosModel>> ExtraerDatosAsync(long criterion, string id)
    {
        var resultado = new ResultadosModel<ImpuestosModel>
        {
            SitioWeb = NombreSitio,
            Status = "Procesado Exitosamente"
        };

        var registrosExtraidos = new List<ImpuestosModel>();

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
        bool exitoNavegacion = false;

        for (int intento = 1; intento <= maxReintentos; intento++)
        {
            try
            {
                await pagina.GotoAsync(UrlSitioWeb, new PageGotoOptions { Timeout = 45000, WaitUntil = WaitUntilState.DOMContentLoaded });
                exitoNavegacion = true;
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

            foreach (var fila in filas)
            {
                var celdas = await fila.Locator("td").AllAsync();
                if (celdas.Count < 2) continue;

                string fecha = (await celdas[0].InnerTextAsync()).Trim();
                if (string.IsNullOrEmpty(fecha) || fecha.Contains("FECHA")) continue;

                if (resultado.convertirHora(fecha) < criterion) continue;

                var celdaObjetivo = celdas[1];
                string textoTitulo = string.Empty;
                string textoId = string.Empty;
                string urlPdf = string.Empty;

                var pElements = await celdaObjetivo.Locator("p").AllAsync();
                if (pElements.Count > 0)
                {
                    textoTitulo = (await pElements[0].InnerTextAsync()).Trim();
                }

                var strongElements = await celdaObjetivo.Locator("strong").AllAsync();
                foreach (var item in strongElements)
                {
                    string inner = (await item.InnerTextAsync()).Trim();
                    if (!string.IsNullOrEmpty(inner))
                    {
                        textoId = inner;
                    }
                }

                if (textoId == id) continue;

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

            resultado.ContenidoIdentificado = registrosExtraidos;
        }
        catch (Exception)
        {
            resultado.Status = "Error en la Extracción del Sitio";
            throw;
        }
        finally
        {
            await contexto.CloseAsync();
        }

        return resultado;
    }
}