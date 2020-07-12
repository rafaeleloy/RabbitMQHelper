﻿using System.Collections.Generic;

namespace RabbitHelper.Consumers
{
    public interface IObjectConsumer : IConsumer
    {

        /// <summary>
        /// The consumer method.
        /// </summary>
        /// <param name="message">The received message.</param>
        bool Consume(object message, Dictionary<string, object> messageProperties);
    }
}
