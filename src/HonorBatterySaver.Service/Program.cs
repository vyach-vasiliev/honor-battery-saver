using HonorBatterySaver.Core;
using HonorBatterySaver.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = PipeProtocol.WindowsServiceName);
builder.Services.AddSingleton<IBatteryRegistry, HonorBatteryRegistry>();
builder.Services.AddSingleton<IOemWmiGateway, HonorOemWmiGateway>();
builder.Services.AddSingleton<IBatteryProfileApplier, HonorWmiBatteryProfileApplier>();
builder.Services.AddSingleton<BatteryServiceController>();
builder.Services.AddSingleton(_ => new RotatingFileLog(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "HonorBatterySaver", "service.log")));
builder.Services.AddHostedService<NamedPipeWorker>();

await builder.Build().RunAsync();
