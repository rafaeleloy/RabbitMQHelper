using RabbitHelper.Events;
using System.Collections.Generic;

namespace RabbitHelper.Publishers
{
    public interface IPublisher
    {
        void Write<T>(T body, string exchange, IDictionary<string, object> headers = null, string messageId = "", string routingKey = "", bool isExclusive = false) where T : IEvent;
        void Write(byte[] body, string exchange, IDictionary<string, object> headers = null, string messageId = "", string routingKey = "", bool isExclusive = false);
        void WriteDefaultExchange(byte[] body, Dictionary<string, object> headers = null);
        void WriteTopic(byte[] body, string messageId, string routingKey, IDictionary<string, object> headers = null);
    }
}
