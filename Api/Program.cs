using OpenTelemetry.Logs;
using Serilog.Sinks.Grafana.Loki;
using Serilog;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using System.Reflection.PortableExecutable;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;

using System.Diagnostics.Metrics;
using Util;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Util.Data;
using Api.Service;

var builder = WebApplication.CreateBuilder(args);
ConfigurationManager configuration = builder.Configuration; // allows both to access and to set up the config
IWebHostEnvironment environment = builder.Environment;

builder.Configuration.AddJsonFile("appsettings.json", true, true)
                    .SetBasePath(environment.ContentRootPath)
                    .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", true, true)
                    .AddEnvironmentVariables();
;

Meter meter = new Meter(Instrumentation.MeterName);
var counter = meter.CreateCounter<int>("contador_metrica", "The number of requests");

builder.Services.AddSingleton(counter);
// Add services to the container.

builder.Services.AddScoped<ApplicationContext>();

builder.Services.AddScoped<IInserirMovimentacaoBancariaService, InserirMovimentacaoBancariaService>();
//

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Logging.ClearProviders();
var resourceBuilder = ResourceBuilder
                       .CreateDefault()
                       .AddService(".Net Log Service");

//builder.Logging.AddOpenTelemetry(logging => {

//    logging.IncludeScopes = true;

//    logging.SetResourceBuilder(resourceBuilder)
//               .AddOtlpExporter(otlpOptions => {

//                   otlpOptions.Protocol = OtlpExportProtocol.Grpc;
//                   otlpOptions.Endpoint = new Uri("https://ingest.in.signoz.cloud:443/");

//                   string headerKey = "signoz-ingestion-key";
//                   string headerValue = "<SIGNOZ_INGESTION_KEY>";

//                   otlpOptions.Headers = $"{headerKey}={headerValue}";
//               });
//});

//var logger = new LoggerConfiguration()
//    .WriteTo.GrafanaLoki("http://localhost:3100", new List<LokiLabel>
//    {
//        new() { Key = "app", Value = "web_app" }
//    }, ["app"])
//    .CreateLogger();

//Log.Logger = logger;

//try
//{
//    for (var i = 0; i < 100; i++)
//    {
//        Log.Information("Information log {i}", i);
//        await Task.Delay(100);
//        Log.Error("Error Log {i}", i);
//        await Task.Delay(100);
//        Log.Debug("Debug log {i}", i);
//        await Task.Delay(100);
//        Console.WriteLine($"Log send {i}");
//    }
//}
//catch (Exception ex)
//{
//    Log.Fatal(ex, "Application terminated unexpectedly");
//}
//finally
//{
//    Log.CloseAndFlush();
//}

var service = new ServiceInformation
{
    ServiceName = builder.Configuration.GetValue("ServiceName", defaultValue: "service-1")!,
    ServiceVersion =  typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    ServiceInstanceId = Environment.MachineName
};

builder.Services.AddSingleton(service); 

builder.Services.AddHttpClient("Api2", http => { 

            http.BaseAddress = new Uri("https://localhost:7155");
            http.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHostedService<InserirMovimentacaoBancariaIntegrationHandler>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
    .AddService(
            serviceName: service.ServiceName,
            serviceVersion: service.ServiceVersion,
            serviceInstanceId: service.ServiceInstanceId))

      .WithLogging(logging =>
      {
          
          logging.AddOtlpExporter(otlpOptions =>
          {
              // Use IConfiguration directly for Otlp exporter endpoint option.
              otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
          });
      })
      .WithTracing(tracing => {

          // Ensure the TracerProvider subscribes to any custom ActivitySources.
          tracing
              .AddSource(Instrumentation.ActivitySourceNameApi1)
              .SetSampler(new AlwaysOnSampler())
              .AddHttpClientInstrumentation()
              .AddSqlClientInstrumentation(opt => {
                  opt.SetDbStatementForText = true; 
              })    
              .AddAspNetCoreInstrumentation();
          tracing.AddOtlpExporter(otlpOptions =>
          {
              // Use IConfiguration directly for Otlp exporter endpoint option.
              otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
          });
      })
      .WithMetrics(metric => {

          metric
           .AddMeter(Instrumentation.MeterName)
           .AddRuntimeInstrumentation()
           .AddHttpClientInstrumentation()
           .AddAspNetCoreInstrumentation();
          
          metric.AddOtlpExporter(otlpOptions =>
          {
              // Use IConfiguration directly for Otlp exporter endpoint option.
              otlpOptions.Endpoint = new Uri(builder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
              otlpOptions.Protocol = OtlpExportProtocol.Grpc;
          });
      })
      ;

builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(
            ResourceBuilder
                .CreateDefault()
                .AddService(
                   serviceName: service.ServiceName,
                   serviceVersion: service.ServiceVersion,
                   serviceInstanceId: service.ServiceInstanceId));
    /*adicionando traces*/
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;
    // TODO: configure export
});


builder.Services.AddSingleton(TracerProvider.Default.GetTracer(service.ServiceName));
builder.Services.AddSingleton<IMessageBusRabbitMq, MessageBusRabbitMq>();
// Registro do DbContext com a string de conexão de log
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationContext>(options =>
                 options.UseSqlServer(connectionString)
                .EnableSensitiveDataLogging()
                .UseLazyLoadingProxies()
    );

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
