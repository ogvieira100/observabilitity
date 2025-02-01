using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Util
{
    public interface IMessageBusRabbitMq : IDisposable
    {
        #region " Mensagens Assincronas "

        void Publish<T>(T message,
                       PropsMessageQueueDto propsMessageQueeDto
                       ) where T : IntegrationEvent;

        void Subscribe<T>(PropsMessageQueueDto propsMessageQueeDto, Action<T> onMessage) where T : IntegrationEvent;

        void SubscribeAsync<T>(PropsMessageQueueDto propsMessageQueeDto, Func<T, Task> onMessage) where T : IntegrationEvent;



        #endregion

        #region " RPC "

        /*usado como publicação envia o request e aguarda o response*/
        TResp RpcSendRequestReceiveResponse<TReq, TResp>(TReq req, PropsMessageQueueDto propsMessageQueeDto) where TReq : IntegrationEvent where TResp : ResponseMessage;

        /*usado como subscription recebe o request processa e envia o response*/
        void RpcSendResponseReceiveRequestAsync<TReq, TResp>(PropsMessageQueueDto propsMessageQueeDto, Func<TReq, Task<TResp>> onMessage) where TReq : IntegrationEvent where TResp : ResponseMessage;


        #endregion
    }
}
