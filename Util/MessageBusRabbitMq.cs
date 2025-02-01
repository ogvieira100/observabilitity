using Microsoft.Extensions.Configuration;
using Polly;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Threading.Channels;
using Azure.Messaging;

namespace Util
{
    public class MessageBusRabbitMq : IMessageBusRabbitMq
    {

        readonly IConfiguration _configuration;
        IConnection _connection;
        IModel _channel = null;
        bool _disposedValue;
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
        public MessageBusRabbitMq(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private void OnDisconnect(object? s, ShutdownEventArgs e)
        {
            var policy = Polly.Policy.Handle<RabbitMQ.Client.Exceptions.RabbitMQClientException>()
                .Or<RabbitMQ.Client.Exceptions.BrokerUnreachableException>()
                .RetryForever();
            policy.Execute(TryConnect);
        }

        private void TryConnect()
        {
            var policy = Polly.Policy.Handle<RabbitMQ.Client.Exceptions.RabbitMQClientException>()
                 .Or<RabbitMQ.Client.Exceptions.BrokerUnreachableException>()
                 .WaitAndRetry(3, retryAttempt =>
                 TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            policy.Execute(() =>
            {
                if (_connection == null
                || (_connection is not null && !_connection.IsOpen))
                {
                    ConnectionFactory factory;

                    factory = new ConnectionFactory()
                    {
                        HostName = _configuration.GetSection("RabbitMq:Host").Value ?? "",
                        UserName = _configuration.GetSection("RabbitMq:UserName").Value ?? "",
                        Password = _configuration.GetSection("RabbitMq:PassWord").Value ?? "",
                        Port = Convert.ToInt32(_configuration.GetSection("RabbitMq:Port").Value ?? "0"),
                        // Ssl = { Enabled = true }
                    };
                    factory.AutomaticRecoveryEnabled = false;
                    factory.RequestedHeartbeat = TimeSpan.FromSeconds(60);
                    _connection = factory.CreateConnection();
                    _connection.ConnectionShutdown += OnDisconnect;

                    if (_channel == null
                    || (_channel is not null && _channel.IsClosed))
                    {
                        _channel = _connection.CreateModel();
                    }
                }
            });
        }



        IDictionary<string, object> DeadLetterQuee(IModel? channel, IDictionary<string, object> args)
        {

            channel?.ExchangeDeclare("DeadLetterExchange", ExchangeType.Fanout);
            channel?.QueueDeclare("DeadLetterQuee", durable: true, exclusive: false, autoDelete: false, null);
            channel?.QueueBind("DeadLetterQuee", "DeadLetterExchange", "");

            var arguments = new Dictionary<string, object>
            {

                {"x-dead-letter-exchange","DeadLetterExchange" }

            };

            foreach (var arg in args)
                arguments.Add(arg.Key, arg.Value);
            return arguments;
        }



        public void Publish<T>(T message
                                        , PropsMessageQueueDto propsMessageQueeDto) where T : IntegrationEvent
        {

            try
            {

                TryConnect();


                var messageSend = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(messageSend);

                using (var chanel = _connection.CreateModel())
                {
                    var args = DeadLetterQuee(chanel, propsMessageQueeDto.Arguments);

                    chanel.QueueDeclare(queue: propsMessageQueeDto.Queue,
                                                   durable: propsMessageQueeDto.Durable,
                                                   exclusive: propsMessageQueeDto.Exclusive,
                                                   autoDelete: propsMessageQueeDto.AutoDelete,
                                                   arguments: args);


                    var activityName = $"{propsMessageQueeDto.Queue} send";


                    ActivitySource ActivitySource = new ActivitySource(propsMessageQueeDto.ServiceName,
                        propsMessageQueeDto.ServiceVersion);

                    using var activity = ActivitySource
                     .StartActivity(activityName, ActivityKind.Producer);

                    var properties = chanel.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.Expiration = "86400000"; // TTL de 1 dia (em milissegundos)

                    ActivityContext contextToInject = default;
                    if (activity != null)
                    {
                        contextToInject = activity.Context;
                    }
                    else if (Activity.Current != null)
                    {
                        contextToInject = Activity.Current.Context;
                    }

                    Propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), properties,
                        InjectTraceContextIntoBasicProperties);

                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination_kind", "queue");
                    activity?.SetTag("messaging.rabbitmq.routing_key", propsMessageQueeDto.Queue);
                    activity?.SetTag("message", messageSend);


                    chanel.BasicPublish(exchange: "",
                                         routingKey: propsMessageQueeDto.Queue,
                                         basicProperties: properties,
                                         body: body);
                }

            }
            catch (Exception ex)
            {

                throw;
            }


        }

        private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
        {
            try
            {
                if (props.Headers == null)
                {
                    props.Headers = new Dictionary<string, object>();
                }

                props.Headers[key] = value;
            }
            catch (Exception ex)
            {
                //this._logger.LogError(ex, "Failed to inject trace context.");
            }
        }

        private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
        {
            try
            {
                if (props.Headers.TryGetValue(key, out var value))
                {
                    var bytes = value as byte[];
                    return new[] { Encoding.UTF8.GetString(bytes!) };
                }
            }
            catch (Exception ex)
            {
               // _logger.LogError(ex, $"Falha durante a extração do trace context: {ex.Message}");
            }

            return Enumerable.Empty<string>();
        }


        /*inscricoes que executam de forma assincrona ou não*/

        public void Subscribe<T>(PropsMessageQueueDto propsMessageQueeDto,
            Action<T> onMessage) where T : IntegrationEvent
        {

            TryConnect();
            var args = DeadLetterQuee(_channel, propsMessageQueeDto.Arguments);
            _channel.QueueDeclare(queue: propsMessageQueeDto.Queue,
                                                durable: propsMessageQueeDto.Durable,
                                                exclusive: propsMessageQueeDto.Exclusive,
                                                autoDelete: propsMessageQueeDto.AutoDelete,
                                                arguments: args);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (sender, ea) =>
            {

                try
                {

                    // Extrai o PropagationContext de forma a identificar os message headers
                    var parentContext = Propagator.Extract(default, ea.BasicProperties, 
                                    this.ExtractTraceContextFromBasicProperties);
                    Baggage.Current = parentContext.Baggage;

                    // Semantic convention - OpenTelemetry messaging specification:
                    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#span-name
                    var activityName = $"{ea.RoutingKey} receive";

                    ActivitySource ActivitySource = new ActivitySource(propsMessageQueeDto.ServiceName,
                       propsMessageQueeDto.ServiceVersion);


                    // Criar um novo span no contexto do trace original
                    using var activity = ActivitySource
                        .StartActivity(activityName, ActivityKind.Consumer, parentContext.ActivityContext);

                   
                    var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var messageSerializable = JsonConvert.DeserializeObject<T>(message);
                        var msgLog = JsonConvert.SerializeObject(messageSerializable);
                  
                    
                    activity?.SetTag("message", msgLog);
                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination_kind", "queue");
                    activity?.SetTag("messaging.rabbitmq.routing_key", propsMessageQueeDto.Queue);


                    onMessage.Invoke(messageSerializable);

                        // Note: it is possible to access the channel via
                        //       ((EventingBasicConsumer)sender).Model here
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    
                }
                catch (Exception ex)
                {
                    //_channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    /*não dá reentrada na fila pois tem tratamento de fila no DeadLetterQuee Quee */
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }

            };
            _channel.BasicConsume(queue: propsMessageQueeDto.Queue,
                                 autoAck: false,
                                 consumer: consumer);
        }

        public void SubscribeAsync<T>(PropsMessageQueueDto propsMessageQueeDto,
                                      Func<T, Task> onMessage) where T : IntegrationEvent
        {
            TryConnect();
            var args = DeadLetterQuee(_channel, propsMessageQueeDto.Arguments);
            _channel.QueueDeclare(queue: propsMessageQueeDto.Queue,
                                                 durable: propsMessageQueeDto.Durable,
                                                 exclusive: propsMessageQueeDto.Exclusive,
                                                 autoDelete: propsMessageQueeDto.AutoDelete,
                                                 arguments: args);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (sender, ea) =>
            {

                try
                {

                    // Extrai o PropagationContext de forma a identificar os message headers
                    var parentContext = Propagator.Extract(default, ea.BasicProperties,
                                    this.ExtractTraceContextFromBasicProperties);
                    Baggage.Current = parentContext.Baggage;

                    // Semantic convention - OpenTelemetry messaging specification:
                    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/messaging.md#span-name
                    var activityName = $"{ea.RoutingKey} receive";

                    ActivitySource ActivitySource = new ActivitySource(propsMessageQueeDto.ServiceName,
                       propsMessageQueeDto.ServiceVersion);


                    // Criar um novo span no contexto do trace original
                    using var activity = ActivitySource
                        .StartActivity(activityName, ActivityKind.Consumer, parentContext.ActivityContext);



                    var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var input = JsonConvert.DeserializeObject<T>(message);
                        var msgLog = JsonConvert.SerializeObject(input);

                    activity?.SetTag("message", msgLog);
                    activity?.SetTag("messaging.system", "rabbitmq");
                    activity?.SetTag("messaging.destination_kind", "queue");
                    activity?.SetTag("messaging.rabbitmq.routing_key", propsMessageQueeDto.Queue);


                    onMessage.Invoke(input).Wait();

                        // Note: it is possible to access the channel via
                        //       ((EventingBasicConsumer)sender).Model here
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    
                }
                catch (Exception ex)
                {
                    //_channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    /*não dá reentrada na fila pois tem tratamento de fila no DeadLetterQuee Quee */
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }

            };
            _channel.BasicConsume(queue: propsMessageQueeDto.Queue,
                                 autoAck: false,
                                 consumer: consumer);
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                    if (_channel != null && _channel.IsOpen)
                    {
                        try
                        {
                            _channel.Close();
                            _channel.Dispose();
                        }
                        catch { }
                    }
                    if (_connection != null && _connection.IsOpen)
                    {
                        try
                        {
                            _connection.Close();
                            _connection.Dispose();
                        }
                        catch { }
                    }
                }
                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }



        /*usado como publicação envia o request e aguarda o response*/
        public TResp RpcSendRequestReceiveResponse<TReq, TResp>(TReq req,
                                                                PropsMessageQueueDto propsMessageQueeDto)
            where TReq : IntegrationEvent
            where TResp : ResponseMessage
        {
            try
            {
                TryConnect();
                TResp resp = default(TResp);
                var processed = false;


                var _channelRpc = _connection.CreateModel();


                _channelRpc.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                var args = DeadLetterQuee(_channelRpc, propsMessageQueeDto.Arguments);


                var replayQueue = $"{propsMessageQueeDto.Queue}_return";
                var correlationId = Guid.NewGuid().ToString();

                _channelRpc.QueueDeclare(queue: replayQueue,
                                     durable: propsMessageQueeDto.Durable,
                                     exclusive: propsMessageQueeDto.Exclusive,
                                     autoDelete: propsMessageQueeDto.AutoDelete,
                                     arguments: args);

                _channelRpc.QueueDeclare(queue: propsMessageQueeDto.Queue,
                                      durable: propsMessageQueeDto.Durable,
                                      exclusive: propsMessageQueeDto.Exclusive,
                                      autoDelete: propsMessageQueeDto.AutoDelete,
                                      arguments: args);


                var consumer = new EventingBasicConsumer(_channelRpc);

                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        if (correlationId == ea.BasicProperties.CorrelationId)
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);
                            resp = JsonConvert.DeserializeObject<TResp>(message);
                            _channelRpc.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                            processed = true;
                        }
                        else
                            _channelRpc.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                    catch (Exception ex)
                    {
                        processed = true;
                        _channelRpc.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                };

                _channelRpc.BasicConsume(queue: replayQueue, autoAck: false, consumer: consumer);

                /**/

                var pros = _channelRpc.CreateBasicProperties();

                pros.CorrelationId = correlationId;
                pros.ReplyTo = replayQueue;
                pros.Expiration = propsMessageQueeDto.TimeoutMensagem.ToString();
                var message = JsonConvert.SerializeObject(req);
                var body = Encoding.UTF8.GetBytes(message);

                _channelRpc.BasicPublish(exchange: "",
                                           routingKey: propsMessageQueeDto.Queue,
                                           basicProperties: pros,
                                           body: body);


                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                while (!processed)
                {
                    if (stopwatch.ElapsedMilliseconds >= propsMessageQueeDto.TimeoutMensagem)
                        return resp;
                }

                return resp;
            }
            catch (Exception ex)
            {

                throw;
            }

        }
        /*fim*/

        public void RpcSendResponseReceiveRequestAsync<TReq, TResp>(
                                                                    PropsMessageQueueDto propsMessageQueeDto,
                                                                    Func<TReq, Task<TResp>> onMessage)
            where TReq : IntegrationEvent where TResp : ResponseMessage
        {
            try
            {
                TryConnect();



                var _channelRpcRespond = _connection.CreateModel();
                _channelRpcRespond.BasicQos(0, 1, false);
                var args = DeadLetterQuee(_channelRpcRespond, propsMessageQueeDto.Arguments);
                _channelRpcRespond.QueueDeclare(queue: propsMessageQueeDto.Queue,
                                         durable: propsMessageQueeDto.Durable,
                                         exclusive: propsMessageQueeDto.Exclusive,
                                         autoDelete: propsMessageQueeDto.AutoDelete,
                                         arguments: args);



                var consumer = new EventingBasicConsumer(_channelRpcRespond);



                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        string response = null;
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var messageSerializable = JsonConvert.DeserializeObject<TReq>(message);
                        var msgLog = JsonConvert.SerializeObject(messageSerializable);

                        var resp = (onMessage?.Invoke(messageSerializable)).Result;
                        response = JsonConvert.SerializeObject(resp);

                        var props = ea.BasicProperties;
                        var replyProps = _channelRpcRespond.CreateBasicProperties();
                        replyProps.CorrelationId = props.CorrelationId;
                        replyProps.Expiration = propsMessageQueeDto.TimeoutMensagem.ToString();

                        var responseBytes = Encoding.UTF8.GetBytes(response);

                        _channelRpcRespond.BasicPublish(exchange: "", routingKey: props.ReplyTo,
                            basicProperties: replyProps, body: responseBytes);

                        _channelRpcRespond.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception err)
                    {
                        _channelRpcRespond.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                };

                _channelRpcRespond.BasicConsume(queue: propsMessageQueeDto.Queue,
                                        autoAck: false,
                                        consumer: consumer);
            }
            catch (Exception ex)
            {

                throw;
            }



        }
        /*fim*/

    }
}
