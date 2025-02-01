using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Util;
using Util.Data;
using Util.Model;

namespace Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        readonly Counter<int> _contador;

        private readonly ILogger<WeatherForecastController> _logger;

        
        readonly HttpClient _httpClientApi2;
        readonly Tracer _tracer;
        readonly ApplicationContext _applicationContext;
        public WeatherForecastController(ApplicationContext applicationContext, Tracer tracer, IHttpClientFactory httpClientFactory,  Counter<int> contador, ILogger<WeatherForecastController> logger)
        {
            _tracer = tracer;
            _applicationContext = applicationContext;   
            _logger = logger;
            _contador = contador;
            _httpClientApi2 =  httpClientFactory.CreateClient("Api2"); 
        }

        [HttpPost("CadastrarCliente")]
        public async Task<IActionResult> CadastrarCliente([FromBody] CustomerRequest cliente)
        {
            try
            {
           
                var clienteAdd = new Cliente
                {
                    Nome = cliente.Nome,
                    CPF = cliente.CPF
                };  
                await _applicationContext.AddAsync(clienteAdd);
                await _applicationContext.SaveChangesAsync();
                cliente.Id = clienteAdd.Id;
                var content = new StringContent(JsonConvert.SerializeObject(cliente), Encoding.UTF8, "application/json");
                var resp =   await _httpClientApi2.PostAsync("WeatherForecast/CadastrarEndereco", content);
                if (resp.IsSuccessStatusCode)
                {
                    return Ok(clienteAdd);
                }

                return BadRequest(clienteAdd); 
            }
            catch (Exception)
            {

                throw;
            }
        
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public  async Task<IEnumerable<WeatherForecast>> Get()
        {
            _logger.LogWarning("GetWeatherForecast called");


            for (var i = 0; i < 100; i++)
            {
                _contador.Add(1);

             

                // _logger.LogInformation("Trace trace {i}", System.Diagnostics.ActivityTraceId);

                _logger.LogInformation("Information log {i}", i);

                _logger.LogError("Error Log {i}", i);

                _logger.LogDebug("Debug log {i}", i);
                
                Console.WriteLine($"Log send {i}");
            }


            var tId = OpenTelemetry.Trace.Tracer.CurrentSpan.Context.TraceId.ToString();
              var spanId =  OpenTelemetry.Trace.Tracer.CurrentSpan.Context.SpanId.ToString();

            var tracerData = new TracerData
            {
                TraceId = tId, // TraceParent do W3C
                TraceState = Activity.Current?.TraceStateString,
                SpanId = spanId

                // TraceState, se disponível
            };
            //var contentTracer = JsonConvert.SerializeObject(_tracer);
            var content = new StringContent(JsonConvert.SerializeObject(tracerData), Encoding.UTF8, "application/json");

            //var track =  TracerProvider.Default.GetTracer("service-1");

            //var traceCont = OpenTelemetry.Trace.Tracer.CurrentSpan.Context;

            //var hh =  JsonConvert.SerializeObject(traceCont);

            //        var trace =  OpenTelemetry.Trace.Tracer.CurrentSpan.Context.TraceId;
            //var traceId =  trace.ToString();   

            var resp = await _httpClientApi2.PostAsync("WeatherForecast/PostWeatherForecast2Trace", content);
            if (resp.IsSuccessStatusCode)
            {

            }


            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
