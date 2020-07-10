using RabbitHelper.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RabbitHelper.Queues
{
    public class QueueHelper
    {
        /// <summary>
        /// This methods declare a queue with ObjectConsumerType name.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        public static void DeclareInheritedQueues(IModel channel)
        {
            foreach (var queue in Assembly.GetEntryAssembly().GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IEvent))).Select(x => x.Name))
                channel.QueueDeclare(queue, true, false, false, null);
        }

        /// <summary>
        /// This methods declare a queue with ObjectConsumerType name.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="durable">If the queue would be durable or not.</param>
        /// <param name="isExclusive">If the queue is exclusive.</param>
        /// <param name="autoDelete">If the queue will delete itself after connection.</param>
        /// <param name="arguments">The queue arguments.</param>
        public static void DeclareInheritedQueues(IModel channel, bool durable, bool isExclusive, bool autoDelete, IDictionary<string, object> arguments)
        {
            foreach (var queue in Assembly.GetEntryAssembly().GetTypes().Where(mytype => mytype.GetInterfaces().Contains(typeof(IEvent))).Select(x => x.Name))
                channel.QueueDeclare(queue, durable, isExclusive, autoDelete, arguments);
        }

        /// <summary>
        /// This method declare queue on RabbitMQ.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queueName">The name of the queue to declare.</param>
        /// <param name="isExclusive">If the queue is exclusive.</param>
        public static void DeclareQueue(IModel channel, string queueName, bool isExclusive)
        {
            var arguments = new Dictionary<string, object>();
            var delayArguments = new Dictionary<string, object>();
            var exchangeQueue = "QUEUE/" + queueName + ".master";
            arguments.Add("x-dead-letter-exchange", exchangeQueue);
            arguments.Add("x-dead-letter-routing-key", "delay");
            channel.ExchangeDeclare(exchangeQueue, "direct", true, false, null);
            channel.QueueDeclare(queueName, true, isExclusive, false, arguments);
            channel.QueueBind(queueName, exchangeQueue, "", null);

            if (!isExclusive)
            {
                delayArguments.Add("x-dead-letter-exchange", exchangeQueue);
                delayArguments.Add("x-dead-letter-routing-key", "");
                delayArguments.Add("x-message-ttl", 600000);
                channel.QueueDeclare(queueName + ".delay", true, false, false, delayArguments);
                channel.QueueBind(queueName + ".delay", exchangeQueue, "delay", null);
            }
        }

        /// <summary>
        /// This method declare a Topic queue on RabbitMQ.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queueName">The name of the queue to declare.</param>
        /// <param name="topicNames">The topic name.</param>
        /// <param name="routingKey">The routing key to bind with queue and exchange.</param>
        public static void DeclareTopicQueue(IModel channel, string queueName, List<string> topicNames, string routingKey)
        {
            var arguments = new Dictionary<string, object>();
            var delayArguments = new Dictionary<string, object>();
            var exchangeQueue = $"QUEUE/{queueName}.master";
            arguments.Add("x-dead-letter-exchange", $"QUEUE/{queueName}.master");
            arguments.Add("x-dead-letter-routing-key", "delay");
            channel.QueueDeclare(queueName, true, false, false, arguments);
            channel.ExchangeDeclare(exchangeQueue, "direct", true, false, null);
            channel.QueueBind(queueName, exchangeQueue, "", null);
            foreach (var item in topicNames)
            {
                var exchangeWork = $"TOPIC/{item.ToLower()}.master";
                channel.ExchangeDeclare(exchangeWork, "topic", true, false, null);
                channel.QueueBind(queueName, exchangeWork, routingKey, null);
            }

            delayArguments.Add("x-dead-letter-exchange", exchangeQueue);
            delayArguments.Add("x-dead-letter-routing-key", "");
            delayArguments.Add("x-message-ttl", 600000);
            channel.QueueDeclare(queueName + ".delay", true, false, false, delayArguments);
            channel.QueueBind(queueName + ".delay", exchangeQueue, "delay", null);
        }

        /// <summary>
        /// This method declare a Topic exchange on RabbitMQ.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="topicName">The topic name.</param>
        /// <returns>The topic exchange name.</returns>
        public static string DeclareTopic(IModel channel, string topicName)
        {
            var exchangeWork = "TOPIC/" + topicName + ".master";
            channel.ExchangeDeclare(exchangeWork, "topic", true, false, null);
            return exchangeWork;
        }

        /// <summary>
        /// This method declare a simple exclusive queue.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queueName">The queue name.</param>
        public static void DeclareExclusiveQueue(IModel channel, string queueName)
        {
            channel.QueueDeclare(queueName, true, true, false, null);
        }

        /// <summary>
        /// This method check if the queue exists.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queueName">The queue name.</param>
        /// <returns>Return true if the queue exists or fale if not.</returns>
        public static bool QueueExists(IModel channel, string queueName)
        {
            try
            {
                channel.QueueDeclarePassive(queueName);
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("exclusive"))
                    return true;
                return false;
            }
        }
    }
}
