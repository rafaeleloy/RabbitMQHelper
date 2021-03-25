using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace RabbitHelper.Consumers
{
    public interface IObjectConsumer : IConsumer
    {

        /// <summary>
        /// The consumer method.
        /// </summary>
        /// <param name="message">The received message.</param>
        bool Consume(JObject message, Dictionary<string, object> messageProperties);
    }
}
