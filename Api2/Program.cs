using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Util;
using Util.Data;

var builder = WebApplication.CreateBuilder(args);
ConfigurationManager configuration = builder.Configuration; // allows both to access and to set up the config
IWebHostEnvironment environment = builder.Environment;

builder.Configuration.AddJsonFile("appsettings.json", true, true)
                    .SetBasePath(environment.ContentRootPath)
                    .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", true, true)
                    .AddEnvironmentVariables();
;

Meter meter = new Meter(Instrumentation.MeterName2);
var counter = meter.CreateCounter<int>("contador_metrica", "The number of requests");
builder.Services.AddSingleton(counter);
builder.Services.AddScoped<ApplicationContext>();


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Logging.ClearProviders();
var resourceBuilder = ResourceBuilder
                       .CreateDefault()
                       .AddService(".Net Log Service");

//https://learn.microsoft.com/pt-br/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
//ActivitySource source = new ActivitySource(builder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!, typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown");
//builder.Services.AddSingleton(source);
//
//using (Activity activity = source.StartActivity("SomeWork"))
//{
//    await StepOne();
//    await StepTwo();
//}

//using (Activity activity = source.StartActivity("SomeWork"))
//{
//    activity?.SetTag("foo", foo);
//    activity?.SetTag("bar", bar);
//    await StepOne();
//    await StepTwo();
//}

// Add services to the container.

var service = new ServiceInformation
{
    ServiceName = builder.Configuration.GetValue("ServiceName", defaultValue: "service-2")!,
    ServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    ServiceInstanceId = Environment.MachineName
};

builder.Services.AddSingleton(service);

builder.Services.AddHttpClient("Api3", http => {

    http.BaseAddress = new Uri("https://localhost:7225");
    http.DefaultRequestHeaders.Add("Accept", "application/json");
});

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
              .AddSource(Instrumentation.ActivitySourceNameApi2)
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
           .AddMeter(Instrumentation.MeterName2)
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
