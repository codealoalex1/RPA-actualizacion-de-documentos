using System.Diagnostics;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure;

namespace Rpa.Infraestructura.Azure
{
    public class AzureOpenAi
    {
        private string EndpointModel { get; } = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_ENDPOINT") ?? string.Empty;
        private string ApiKeyModel { get; } = Environment.GetEnvironmentVariable("AZURE_OPEN_AI_APIKEY") ?? string.Empty;
        public string DeploymentName { get; set; } = "Phi-4-mini-instruct";
        public AzureOpenAi() { }
        public async void ChatModel(string userMessage, string systemMessage="", string assistantMessage = "")
        {
            Console.WriteLine("\n[4/4] Conectando con Azure OpenAI para generar el resumen ejecutivo...");

            // Inicializamos el cliente oficial de Azure OpenAI
            var cliente = new AzureOpenAIClient(new Uri(EndpointModel), new AzureKeyCredential(ApiKeyModel));
            var chatCliente = cliente.GetChatClient(DeploymentName);

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
        ChatMessage.CreateUserMessage($"A continuación, te proporciono el texto completo extraído de la circular:\n\n{userMessage}")
    };

            try
            {
                // Realizamos la petición asíncrona al modelo
                ChatCompletion completacion = await chatCliente.CompleteChatAsync(opcionesChat);

                // Retornamos el texto de la respuesta
                Console.WriteLine(completacion.Content[0].Text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al conectar con Azure OpenAI: {ex.Message}");
            }
        }

    }
}