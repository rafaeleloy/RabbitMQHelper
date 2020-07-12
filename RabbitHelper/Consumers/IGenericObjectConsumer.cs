using RabbitHelper.Events;

namespace RabbitHelper.Consumers
{
    public interface IGenericObjectConsumer : IConsumer
    {
        /// <summary>
        /// The consumer method.
        /// </summary>
        /// <param name="message">The received message.</param>
        bool Consume<T>(T message) where T : class, IEvent;
    }
}
