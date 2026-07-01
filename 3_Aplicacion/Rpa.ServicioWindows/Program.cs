using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rpa.Nucleo.Interfaces;
using Rpa.Infraestructura.SitiosWeb;
using Rpa.ServicioWindows;
using Rpa.Infraestructura.Azure;
using Rpa.Nucleo.Modelos;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "RpaDocumentosService";
    })
    .ConfigureServices((hostContext, services) =>
    {
        string cadenaConexion = "DefaultEndpointsProtocol=https;AccountName=agenteactdocumentosst;AccountKey=R8BtwX/0IgjFElw1ZH2hir6w79jy0goZ+WAA2ODYCAJcr2saqenWOPO/w/nrizMICnK0ikR0/Fl6+AStiJgmGg==;EndpointSuffix=core.windows.net";
        string contenedor = "registros-json";
        services.AddSingleton<IAlmacenamientoServicio>(sp =>
            new AlmacenamientoBlob(cadenaConexion, contenedor));

        services.AddTransient<ImpuestosExtractor>();
        services.AddTransient<NormativaMEFP>();
        services.AddTransient<GODecreto>();
        services.AddTransient<GOLeyes>();
        services.AddTransient<BCBCircularesExternas>();
        services.AddTransient<BCBResoluciones>();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();