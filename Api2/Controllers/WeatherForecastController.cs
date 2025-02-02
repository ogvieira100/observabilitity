using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using Util;
using Util.Data;
using Util.Model;

namespace Api2.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        readonly Counter<int> _contador;
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        HttpClient _client;
        readonly Tracer _tracer;
        readonly ApplicationContext _applicationContext;

        public WeatherForecastController(ApplicationContext applicationContext, Tracer tracer, IHttpClientFactory clientFactory, Counter<int> contador, ILogger<WeatherForecastController> logger)
        {
            _applicationContext = applicationContext;
            _tracer = tracer;
            _contador = contador;
            _logger = logger;
            _client = clientFactory.CreateClient("Api3");
        }

        [HttpPost("CadastrarEndereco")]
        public async Task<IActionResult> CadastrarEndereco([FromBody] CustomerRequest cliente)
        {
            try
            {
                var setCliente = _applicationContext.Set<Cliente>();
                var clienteSearch = await setCliente.FirstOrDefaultAsync(x => x.Id == cliente.Id);

                if (clienteSearch != null)
                {

                    _logger.LogInformation($"Cadastrando o endereco do cliente {clienteSearch.Nome}  ");
                    var endResidencial = new Endereco
                    {
                        Bairro = "Centro",
                        Logradouro = "Rua 1",
                        Cep = "12345678"
                    };

                    var endComercial = new Endereco
                    {
                        Bairro = "Centro",
                        Logradouro = "Rua 1",
                        Cep = "12345678",
                        TipoEndereco = TipoEndereco.Comercial
                    };


                    var endCobranca = new Endereco
                    {
                        Bairro = "Centro",
                        Logradouro = "Rua 1",
                        Cep = "12345678",
                        TipoEndereco = TipoEndereco.Cobranca
                    };

                    await _applicationContext.AddAsync(endResidencial);
                    await _applicationContext.AddAsync(endComercial);
                    await _applicationContext.AddAsync(endCobranca);

                    clienteSearch.Enderecos.Add(endResidencial);
                    clienteSearch.Enderecos.Add(endComercial);
                    clienteSearch.Enderecos.Add(endCobranca);
                }
                await _applicationContext.SaveChangesAsync();
                _logger.LogInformation($"Endereco cadastrado com sucesso {(clienteSearch.Nome, Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            PreserveReferencesHandling = PreserveReferencesHandling.Objects
                        })}  ");
                var content = new StringContent(JsonConvert.SerializeObject(cliente), Encoding.UTF8, "application/json");
                _logger.LogInformation($" Transmitindo para a api 3 WeatherForecast/CadastraMovimentacaoBancaria  ");
                var resp = await _client.PostAsync("WeatherForecast/CadastraMovimentacaoBancaria", content);
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation($" transmisão concluida  ");
                    return Ok();
                }
                else
                {
                    var response = await resp.Content.ReadAsStringAsync();
                    _logger.LogError($"falha na  transmisão concluida  {response} ");
                    return BadRequest(response);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"falha na  transmisão concluida {ex.Message}  ");
                return BadRequest(ex.Message);
            }
        }
        [HttpPost("PostWeatherForecast2Trace")]
        public async Task<IEnumerable<WeatherForecast>> PostWeatherForecast2Trace([FromBody] TracerData traco)
        {


            //SpanContext spanContext = new SpanContext(
            //ActivityTraceId.CreateFromString(traco.TraceId),
            //ActivitySpanId.CreateFromString(traco.SpanId),
            //ActivityTraceFlags.None);   


            //using (var activity = _tracer.StartActiveSpan("DoWorkApi2",
            //    SpanKind.Server, 
            //    spanContext))
            //{

            _logger.LogWarning("GetWeatherForecast api2 called");


            for (var i = 0; i < 50; i++)
            {
                _contador.Add(1);

                _logger.LogInformation("Api 2 {i}", i);


            }
            var content = new StringContent(JsonConvert.SerializeObject(traco), Encoding.UTF8, "application/json");

            var resp = await _client.PostAsync("WeatherForecast/PostWeatherForecast2Trace", content);
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
            //           }

        }


        [HttpGet("GetWeatherForecast2")]
        public IEnumerable<WeatherForecast> GetWeatherForecast2()
        {

            _logger.LogWarning("GetWeatherForecast called");


            for (var i = 0; i < 100; i++)
            {
                _contador.Add(1);

                _logger.LogInformation("Information log {i}", i);

                _logger.LogError("Error Log {i}", i);

                _logger.LogDebug("Debug log {i}", i);

                Console.WriteLine($"Log send {i}");
            }
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {

            _logger.LogWarning("GetWeatherForecast called");


            for (var i = 0; i < 100; i++)
            {
                _contador.Add(1);

                _logger.LogInformation("Information log {i}", i);

                _logger.LogError("Error Log {i}", i);

                _logger.LogDebug("Debug log {i}", i);

                Console.WriteLine($"Log send {i}");
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
