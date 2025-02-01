using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

Meter meter = new Meter(Instrumentation.MeterName3);
var counter = meter.CreateCounter<int>("contador_metrica", "The number of requests");
builder.Services.AddScoped<ApplicationContext>();
builder.Services.AddSingleton(counter);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Logging.ClearProviders();
var resourceBuilder = ResourceBuilder
                       .CreateDefault()
                       .AddService(".Net Log Service");

var service = new ServiceInformation
{
    ServiceName = builder.Configuration.GetValue("ServiceName", defaultValue: "service-3")!,
    ServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    ServiceInstanceId = Environment.MachineName
};

builder.Services.AddSingleton(service);
//builder.Services.AddHttpClient("Api2", http => {

//    http.BaseAddress = new Uri("https://localhost:7155");
//    http.DefaultRequestHeaders.Add("Accept", "application/json");
//});

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
              .AddSource(Instrumentation.ActivitySourceNameApi3)
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
           .AddMeter(Instrumentation.MeterName3)
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
