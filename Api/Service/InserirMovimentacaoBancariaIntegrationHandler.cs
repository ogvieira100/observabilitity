
using MediatR;
using System.Security.Cryptography.Xml;
using Util;

namespace Api.Service
{
    public class InserirMovimentacaoBancariaIntegrationHandler : BackgroundService
    {
        readonly IServiceProvider _serviceProvider;
        readonly IMessageBusRabbitMq _messageBusRabbitMq;
        readonly ServiceInformation _serviceInformation;
        readonly ILogger<InserirMovimentacaoBancariaIntegrationHandler> _logger;

        public InserirMovimentacaoBancariaIntegrationHandler(IServiceProvider serviceProvider,
            IMessageBusRabbitMq messageBusRabbitMq,
            ILogger<InserirMovimentacaoBancariaIntegrationHandler> logger,
            ServiceInformation serviceInformation)
        {
            _serviceProvider = serviceProvider;
            _messageBusRabbitMq = messageBusRabbitMq;
            _logger = logger;
            _serviceInformation = serviceInformation;
        }

        void SetResponder()
        {
            _messageBusRabbitMq.SubscribeAsync<InserirMovimentacaoBancariaIntegrationEvent>(
                new PropsMessageQueueDto
                {
                    ServiceName = _serviceInformation.ServiceName,
                    ServiceVersion = _serviceInformation.ServiceVersion,
                    Queue = "QueeInserirMovimentacaoBancaria"
                },
                InserirMovimentacaoBancariaAsync
                );
        }

        async Task InserirMovimentacaoBancariaAsync(InserirMovimentacaoBancariaIntegrationEvent @event)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var _inserirMovimentacaoBancariaService = scope.ServiceProvider.GetRequiredService<IInserirMovimentacaoBancariaService>();
                    await _inserirMovimentacaoBancariaService.InserirMovimentacaoBancariaAsync(new Util.Model.CustomerRequest { Id = @event.ClientId });
                    
                }
            }
            catch (Exception ex)
            {

                _logger.LogError($" Não consegui processar a mensagem {ex.Message} ");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SetResponder();
            return Task.CompletedTask;
        }
    }
}
