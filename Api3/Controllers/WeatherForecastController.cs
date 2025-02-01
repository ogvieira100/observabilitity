using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Util;
using Util.Data;
using Util.Model;

namespace Api3.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        readonly Counter<int> _contador;
        readonly Tracer _tracer;
        readonly IMessageBusRabbitMq _messageBusRabbitMq;
        readonly ApplicationContext _applicationContext;
        readonly ServiceInformation _serviceInformation;
        public WeatherForecastController(ServiceInformation serviceInformation,IMessageBusRabbitMq messageBusRabbitMq,ApplicationContext applicationContext, Tracer tracer, Counter<int> contador, ILogger<WeatherForecastController> logger)
        {
            _serviceInformation = serviceInformation;   
            _messageBusRabbitMq = messageBusRabbitMq;   
            _logger = logger;
            _contador = contador;
            _tracer = tracer;
            _applicationContext = applicationContext;   
        }

        [HttpPost("CadastraMovimentacaoBancaria")]
        public async Task<IActionResult> CadastraMovimentacaoBancaria([FromBody] CustomerRequest customerRequest)
        {

            var client = await _applicationContext.Set<Cliente>().FirstOrDefaultAsync(x => x.Id == customerRequest.Id);
            decimal[] decimals = { 100.50m, -200.50m, 3010.10m, -400m, 500.15m, 60010.50m, 701.55m, -810.50m, 910.55m, -10.00m , 55.10m };
            for (int i = 1; i <= 100; i++)
            {
                var rand  = new Random();
                var pos =  rand.Next(0, (decimals.Length -1) );
                var valPos = decimals[pos]; 
                var tipoMov = valPos > 0 ? TipoMovimentacao.Credito : TipoMovimentacao.Debito;  

                var mov = new MovimentacaoBancaria
                {
                    DataMovimentacao = DateTime.Now,
                    TipoMovimentacao = tipoMov,
                    Valor = valPos
                };

                await _applicationContext.AddAsync(mov);
                client.MovimentacaoBancarias.Add(mov);

            }
            await _applicationContext.SaveChangesAsync();
            /*enviar fila*/
            _messageBusRabbitMq.Publish(new InserirMovimentacaoBancariaIntegrationEvent {
                ClientId = customerRequest.Id,  
            }, new PropsMessageQueueDto {
                ServiceName = _serviceInformation.ServiceName,
                ServiceVersion = _serviceInformation.ServiceVersion,    
                Queue = "QueeInserirMovimentacaoBancaria"
            });      
            return Ok(customerRequest);
        }

        [HttpPost("PostWeatherForecast2Trace")]
        public IEnumerable<WeatherForecast> PostWeatherForecast2Trace([FromBody] TracerData traco)
        {


            //SpanContext spanContext = new SpanContext(
            //    ActivityTraceId.CreateFromString(traco.TraceId),
            //    ActivitySpanId.CreateFromString(traco.SpanId),
            //ActivityTraceFlags.None);

            //using (var activity = _tracer.StartActiveSpan("DoWorkApi3", SpanKind.Server, spanContext))
            //{


                _logger.LogWarning("GetWeatherForecast called");


                for (var i = 0; i < 50; i++)
                {
                    _contador.Add(1);
                    _logger.LogInformation("Api 3 {i}", i);


                }
        
                return Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray();
           // }

        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
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
