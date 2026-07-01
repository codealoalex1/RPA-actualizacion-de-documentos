using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Rpa.Nucleo.Interfaces;
using Rpa.Nucleo.Modelos;

namespace Rpa.Infraestructura.SitiosWeb;

public class ImpuestosExtractor : IExtractorWeb<ImpuestosModel>
{
    // Nombre único que identifica a este extractor en el sistema
    public string NombreSitio => "Servicio de Impuestos Nacionales | Entidad facilitadora del cumplimiento de las obligaciones tributarias";
    public string UrlSitioWeb => "https://www.impuestos.gob.bo/index.php/rnd-2026/";

    public async Task<ResultadosModel<ImpuestosModel>> ExtraerDatosAsync(long dateTime, string id)
    {

        var resultado = new ResultadosModel<ImpuestosModel>
        {
            SitioWeb = NombreSitio,
            Status = "Procesado Exitosamente"
        };

        // Inicializar Playwright y lanzar el navegador en modo oculto (Headless)
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

            // Forzar espera: Garantizar que la tabla y sus filas existan en el DOM antes de continuar
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
                    string textoTitulo = await celdaObjetivo.EvaluateAsync<string>(@"element => {
        const textos = Array.from(element.childNodes)
            .filter(node => node.nodeType === Node.TEXT_NODE)
            .map(node => node.textContent.trim())
            .filter(text => text.length > 0);
            if(textos.length == 1) return textos[0];
            if(textos.length > 1) return textos[0]+textos[1];
            if(textos.length <= 0) return ''; 
    }");
                    // Condicional para verificar cuales son los documentos más recientes
                    // Cambiar DateTime.Today.Ticks por la fecha a evaluar 
                    if (resultado.convertirHora(fecha) < dateTime || textoTitulo == id)
                    {
                        continue;
                    }



                    textoTitulo = textoTitulo.Replace("\"", "").Trim();

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

                    var nuevoRegistro = new ImpuestosModel(NombreSitio)
                    {
                        Titulo = textoTitulo,
                        Id = textoId.Trim(),
                        UrlPdf = urlPdf.Trim(),
                        Fecha = fecha
                    };

                    registrosExtraidos.Add(nuevoRegistro);
                }
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