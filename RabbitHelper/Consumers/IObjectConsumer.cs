using RabbitHelper.Events;
using System.Collections.Generic;

namespace RabbitHelper.Consumers
{
    public interface IObjectConsumer<T> : IConsumer where T : class, IEvent
    {
        /// <summary>
        /// The consumer method.
        /// </summary>
        /// <param name="message">The received message.</param>
        public bool Consume(T message);

        /// <summary>
        /// The consumer method.
        /// </summary>
        /// <param name="message">The received message.</param>
        public bool Consume(object message, Dictionary<string, object> messageProperties);
    }
}
