using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rpa.Nucleo.Interfaces;
using Rpa.Infraestructura.SitiosWeb;
using Rpa.ServicioWindows;
using Rpa.Infraestructura.Azure;
using Rpa.Nucleo.Modelos;
using Rpa.Infraestructura.Ocr;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "RpaDocumentosService";
    })
    .ConfigureServices(static async (hostContext, services) =>
    {
        /* string cadenaConexion = "DefaultEndpointsProtocol=https;AccountName=agenteactdocumentosst;AccountKey=R8BtwX/0IgjFElw1ZH2hir6w79jy0goZ+WAA2ODYCAJcr2saqenWOPO/w/nrizMICnK0ikR0/Fl6+AStiJgmGg==;EndpointSuffix=core.windows.net";
        string contenedor = "registros-json";
        services.AddSingleton<IAlmacenamientoServicio>(sp =>
            new AlmacenamientoBlob(cadenaConexion, contenedor));

        services.AddTransient<ImpuestosExtractor>();
        services.AddTransient<NormativaMEFP>();
        services.AddTransient<GODecreto>();
        services.AddTransient<GOLeyes>();
        services.AddTransient<BCBCircularesExternas>();
        services.AddTransient<BCBResoluciones>();
        services.AddHostedService<Worker>(); */
        try
        {
            Ocr ocr = new Ocr() { };
            /* ocr.Pdf = "https://servdmzw.asfi.gob.bo/circular/Circulares/ASFI_960.pdf#[0,{%22name%22:%22FitR%22},-360,-5,973,792]"; */
            string rutaPdf = "/workspaces/RPA-actualizacion-de-documentos/ASFI_960.pdf";
            string tessDataPath = ocr.GetTessDataPath();
            /* byte[] pdfByte = await ocr.ObtenerPdfDesdeUrl(ocr.Pdf);
            Console.WriteLine(pdfByte); */
            var resultado = ocr.ConvertirPdfAImagenes3(rutaPdf);
            string texto = ocr.ProcesarOcrLocal(resultado, tessDataPath);
            Console.WriteLine(texto);
            string rutaAlmac = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resultado.txt");
            File.WriteAllText(rutaAlmac, texto);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        /* AzureOpenAi azureOpenAi = new();
        string userMessage = "Recomiendame lugares donde haya un clima cálido en La Paz, Bolivia, especificamente en el centro de la ciudad";
        azureOpenAi.ChatModel(userMessage); */

        Environment.Exit(0);
    })
    .Build();

await host.RunAsync();