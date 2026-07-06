using System.Runtime.InteropServices;
using System.Text;
using Azure;
using Azure.AI.OpenAI;
using Docnet.Core;
using Docnet.Core.Models;
using OpenAI.Chat;
using SkiaSharp;
using Tesseract;

namespace Rpa.Infraestructura.Ocr
{
    public class Ocr
    {
        public string Pdf { get; set; } = "";
        public string Image { get; set; } = "";

        /* Obtener las imagenes de los pdfs */
        private static List<byte[]> ConvertirPdfAImagenes(string rutaPdf)
        {
            var listaImagenes = new List<byte[]>();

            if (!File.Exists(rutaPdf))
                throw new FileNotFoundException($"No se encontró el archivo PDF: {rutaPdf}");

            var docReader = DocLib.Instance;

            // DEFINICIÓN DE RESOLUCIÓN HD (Equivalente a un escaneo limpio de 300 DPI para hojas A4/Carta)
            // Esto garantiza que el texto de la ASFI se extraiga gigante y completamente legible.
            var dimensionesAltaDefinicion = new PageDimensions(2479, 3508);

            // LLAMADA CORREGIDA: Pasamos obligatoriamente la ruta y el objeto PageDimensions para solucionar el CS1501
            using (var docHandler = docReader.GetDocReader(rutaPdf, dimensionesAltaDefinicion))
            {
                int conteoPaginas = docHandler.GetPageCount();

                for (int i = 0; i < conteoPaginas; i++)
                {
                    using (var pageHandler = docHandler.GetPageReader(i))
                    {
                        // Obtenemos el ancho y alto real asignado a esta página escalada
                        int width = pageHandler.GetPageWidth();
                        int height = pageHandler.GetPageHeight();

                        // Extraemos los bytes usando la sobrecarga base de flags
                        var rawBytes = pageHandler.GetImage((RenderFlags)0);

                        // Cálculo exacto del Stride basado en las dimensiones reales de lectura
                        int bytesPerRow = width * 4;

                        var srcInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var dstInfo = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);

                        using (var bitmapOrigen = new SKBitmap())
                        using (var bitmapDestino = new SKBitmap(dstInfo))
                        using (var canvas = new SKCanvas(bitmapDestino))
                        {
                            unsafe
                            {
                                fixed (byte* pBytes = rawBytes)
                                {
                                    bitmapOrigen.InstallPixels(srcInfo, (IntPtr)pBytes, bytesPerRow);
                                }
                            }

                            // Forzamos el fondo blanco nítido para un contraste absoluto
                            canvas.Clear(SKColors.White);
                            canvas.DrawBitmap(bitmapOrigen, 0, 0);
                            canvas.Flush();

                            using (var image = SKImage.FromBitmap(bitmapDestino))
                            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    data.SaveTo(memoryStream);
                                    listaImagenes.Add(memoryStream.ToArray());
                                }
                            }
                        }
                    }
                }
            }

            return listaImagenes;
        }

        /* Procesar cada una de las imagenes de img a lista de bytes */
        private static string ProcesarOcrLocal(List<byte[]> imagenes, string rutaTessData)
        {
            var textoAcumulado = new StringBuilder();

            /* Crear carpeta de texto */
            string carpetaDebug = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textos_pdf");
            if (!Directory.Exists(carpetaDebug)) Directory.CreateDirectory(carpetaDebug);

            // Inicializamos el motor físico de Tesseract apuntando a nuestros diccionarios y fijando el idioma "spa"
            using (var motorOcr = new TesseractEngine(rutaTessData, "spa", EngineMode.Default))
            {
                int paginaActual = 1;
                string rutaTxtResultado = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resultado_OCR0.txt");
                /* int op = 0; */

                motorOcr.SetVariable("tessedit_pageseg_mode", "3");
                motorOcr.SetVariable("preserve_interword_spaces", "1");
                foreach (var bytesImagen in imagenes)
                {
                    using (var pixImage = Pix.LoadFromMemory(bytesImagen))
                    {
                        using (var paginaProcesada = motorOcr.Process(pixImage))
                        {
                            string textoDeLaPagina = paginaProcesada.GetText();
                            textoAcumulado.AppendLine($"--- INICIO PÁGINA {paginaActual} ---");
                            textoAcumulado.AppendLine(textoDeLaPagina);
                            textoAcumulado.AppendLine($"--- FIN PÁGINA {paginaActual} ---\n");
                            paginaActual++;
                        }
                    }

                    /* Añadir documentos resultado en .txt */
                    /* if (paginaActual % 5 == 0)
                    {
                        op = (paginaActual / 5) - 1;
                        if (File.Exists(rutaTxtResultado)) File.Delete(rutaTxtResultado);
                        File.WriteAllText(rutaTxtResultado, textoAcumulado.ToString(), Encoding.UTF8);
                        File.Move(rutaTxtResultado, $"{carpetaDebug}\\Resultado_OCR{op}.txt");
                        rutaTxtResultado = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Resultado_OCR{paginaActual / 5}.txt");
                        textoAcumulado = new StringBuilder();
                    }
                    if (paginaActual - 1 == imagenes.Count)
                    {
                        op++;
                        File.WriteAllText(rutaTxtResultado, textoAcumulado.ToString(), Encoding.UTF8);
                        File.Move(rutaTxtResultado, $"{carpetaDebug}\\Resultado_OCR{op}.txt");
                    } */
                }
            }

            return textoAcumulado.ToString();
        }
        
        /* Procesar los byes de la imagen */
        private static string ProcesarOcrImagen(byte[] bytesImagen, string rutaTessData)
        {
            Console.WriteLine("[1/2] Re-renderizando imagen en formato de alta definición (Lienzo Limpio)...");

            // 1. Cargamos la imagen original de la cámara
            using var bitmapOriginal = SKBitmap.Decode(bytesImagen);

            // 2. Escalamos la factura a un ancho HD nativo para que las letras pequeñas tomen cuerpo
            int width = 2500;
            float factorEscala = (float)width / bitmapOriginal.Width;
            int height = (int)(bitmapOriginal.Height * factorEscala);

            // Definimos la información de color emulando tu lógica exitosa de PDF (Rgb888x / Opaque)
            var dstInfo = new SKImageInfo(width, height, SKColorType.Rgb888x, SKAlphaType.Opaque);

            using var bitmapDestino = new SKBitmap(dstInfo);
            using (var canvas = new SKCanvas(bitmapDestino))
            {
                // TRUCO CLAVE DEL PDF: Forzamos el fondo blanco nítido para un contraste absoluto antes de pintar
                canvas.Clear(SKColors.White);

                // Muestreo cúbico de alta fidelidad para suavizar bordes texturizados sin empastar el subrayado
                var opcionesMuestreo = new SKSamplingOptions(SKCubicResampler.CatmullRom);
                // Dibujamos la imagen original sobre el nuevo lienzo plano
                canvas.DrawBitmap(bitmapOriginal, new SKRect(0, 0, width, height), opcionesMuestreo);
                canvas.Flush();
            }

            // 3. Convertimos a PNG de alta calidad sin pérdidas (Exactamente como tu flujo de PDF)
            using var image = SKImage.FromBitmap(bitmapDestino);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            byte[] bytesProcesados = data.ToArray();

            // Guardamos la imagen limpia de depuración para control visual
            string rutaDebug = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "factura_limpia_debug.png");
            File.WriteAllBytes(rutaDebug, bytesProcesados);

            // --- PASO 2: EXTRACCIÓN CON TESSERACT OCR ---
            Console.WriteLine("[2/2] Procesando caracteres con Tesseract local...");
            var textoExtraido = new StringBuilder();

            try
            {
                using var motorOcr = new TesseractEngine(rutaTessData, "spa", EngineMode.Default);

                // Al usar un lienzo limpio, PSM 3 (Bloque uniforme por defecto) o PSM 6 se vuelven sumamente eficientes
                motorOcr.SetVariable("tessedit_pageseg_mode", "3");
                motorOcr.SetVariable("preserve_interword_spaces", "1");

                using var pixImage = Pix.LoadFromMemory(bytesProcesados);
                using var paginaProcesada = motorOcr.Process(pixImage);

                string textoCrudo = paginaProcesada.GetText();
                textoExtraido.AppendLine(textoCrudo);
            }
            catch (Exception ex)
            {
                textoExtraido.AppendLine($"Error en el motor OCR local: {ex.Message}");
            }

            return textoExtraido.ToString();
        }

        /* Enviar el texto extraido de las imagenes al modelo en Azure Open Ai */
        private static async Task<string> GenerarResumenConOpenAIAsync(string textoOcr, string
        azureEndpoint, string azureApiKey, string deploymentName)
        {
            Console.WriteLine("\n[4/4] Conectando con Azure OpenAI para generar el resumen ejecutivo...");

            // Inicializamos el cliente oficial de Azure OpenAI
            var cliente = new AzureOpenAIClient(new Uri(azureEndpoint), new AzureKeyCredential(azureApiKey));
            var chatCliente = cliente.GetChatClient(deploymentName);

            // Definimos el comportamiento experto del modelo mediante el System Prompt
            string systemPrompt = @"Eres un asistente legal y financiero experto en la regulación bancaria de la ASFI en Bolivia. 
Tu tarea es leer el texto extraído mediante OCR de una circular oficial y generar un resumen ejecutivo estructurado y profesional de todo su contenido.

El resumen DEBE incluir obligatoriamente los siguientes puntos:
1. IDENTIFICACIÓN: Número de Circular, Fecha de emisión y Autoridades firmantes.
2. OBJETO PRINCIPAL: Qué reglamentos, manuales o recopilación de normas se están modificando o aprobando.
3. ASPECTOS CLAVE: Resumen analítico de los puntos más importantes tratados (si habla de tarifas, comisiones, tasas de interés, plazos, etc.).
4. ACCIONES REQUERIDAS: Qué deben hacer las entidades financieras o el mercado a partir de esta publicación y vigencia.

Presenta la información de forma clara, limpia, usando viñetas y títulos en Markdown, ideal para ser leído por directores o auditores.";

            var opcionesChat = new List<ChatMessage>
    {
        ChatMessage.CreateSystemMessage(systemPrompt),
        ChatMessage.CreateUserMessage($"A continuación, te proporciono el texto completo extraído de la circular:\n\n{textoOcr}")
    };

            try
            {
                // Realizamos la petición asíncrona al modelo
                ChatCompletion completacion = await chatCliente.CompleteChatAsync(opcionesChat);

                // Retornamos el texto de la respuesta
                return completacion.Content[0].Text;
            }
            catch (Exception ex)
            {
                return $"Error al conectar con Azure OpenAI: {ex.Message}";
            }
        }
        
        /* Definir que sistema operativo se está utilizando Linux o Windows */
        private static string GetTessDataPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return @"C:\Program Files\Tesseract-OCR\tessdata";
            }
            else
            {
                return "/usr/share/tesseract-ocr/4.00/tessdata";
            }
        }
    }
}