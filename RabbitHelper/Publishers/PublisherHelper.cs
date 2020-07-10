using Newtonsoft.Json;
using RabbitHelper.Connector;
using RabbitHelper.Events;
using RabbitHelper.Queues;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace RabbitHelper.Publishers
{
    public class PublisherHelper
    {
        private readonly QueueParametersGeneric parameters;
        private static readonly Dictionary<string, bool> hasQueue = new Dictionary<string, bool>(5000);

        private static IConnection rabbitConnection;
        private static readonly object lockRabbitConnection = new object();

        private string QueueOrTopicName { get; set; }
        private string Exchange { get; set; }

        public PublisherHelper(string queueNameOrTopicName)
        {
            parameters = new QueueParametersGeneric();
            QueueOrTopicName = queueNameOrTopicName;
        }

        /// <summary>
        /// Send a message to exchange.
        /// </summary>
        /// <param name="body">The message to send.</param>
        /// <param name="messageId">The message id.</param>
        /// <param name="exchange">The exchange to send the message.</param>
        /// <param name="routingKey">The message routing key.</param>
        public void Write<T>(T body, string exchange, IDictionary<string, object> headers = null, string messageId = "", string routingKey = "", bool isExclusive = false) where T : IEvent
        {
            ConnectRabbitMQ();

            using (var channel = rabbitConnection.CreateModel())
            {
                Exchange = exchange;

                if (!hasQueue.ContainsKey(QueueOrTopicName))
                {
                    QueueHelper.DeclareQueue(channel, QueueOrTopicName, isExclusive);
                    hasQueue[QueueOrTopicName] = true;
                }

                WriteToExchange(channel, body, messageId, routingKey, headers);
            }
        }

        /// <summary>
        /// Send a message to exchange.
        /// </summary>
        /// <param name="body">Message in bytes.</param>
        /// <param name="messageId">The message id.</param>
        /// <param name="exchange">The exchange to send the message.</param>
        /// <param name="routingKey">The message routing key.</param>
        public void Write(byte[] body, string exchange, IDictionary<string, object> headers = null, string messageId = "", string routingKey = "", bool isExclusive = false)
        {
            ConnectRabbitMQ();

            using (var channel = rabbitConnection.CreateModel())
            {
                Exchange = exchange;

                if (!hasQueue.ContainsKey(QueueOrTopicName))
                {
                    QueueHelper.DeclareQueue(channel, QueueOrTopicName, isExclusive);
                    hasQueue[QueueOrTopicName] = true;
                }

                WriteToExchange(channel, body, messageId, routingKey, headers);
            }
        }

        /// <summary>
        /// Send a message to exclusive queue with exchange default.
        /// </summary>
        /// <param name="body">Message in bytes.</param>
        public void WriteDefaultExchange(byte[] body, Dictionary<string, object> headers = null)
        {
            ConnectRabbitMQ();

            Exchange = "";

            using (var channel = rabbitConnection.CreateModel())
            {
                WriteToExchange(channel, body, "", "", headers);
            }
        }

        /// <summary>
        /// Send message to topic that would have the name in the pattern "TOPIC/domain_entity.master", using a routing key in pattern
        /// "operation.primaryKeys.version".
        /// </summary>
        /// <param name="body">Message in bytes.</param>
        /// <param name="routingKey">The message routing key.</param>
        public void WriteTopic(byte[] body, string messageId, string routingKey, IDictionary<string, object> headers = null)
        {
            ConnectRabbitMQ();
            using (var channel = rabbitConnection.CreateModel())
            {
                if (!hasQueue.ContainsKey(QueueOrTopicName))
                {
                    var exchange = QueueHelper.DeclareTopic(channel, QueueOrTopicName.ToLower());
                    hasQueue[QueueOrTopicName] = true;
                    Exchange = exchange;
                }

                WriteToExchange(channel, body, messageId, routingKey, headers);
            }
        }

        private void WriteToExchange(IModel channel, byte[] body, string messageId, string routingKey, IDictionary<string, object> headers)
        {
            var properties = channel.CreateBasicProperties();

            string guid = System.Guid.NewGuid().ToString();

            properties.Headers = headers;

            properties.DeliveryMode = 2; //persistent

            if (string.IsNullOrEmpty(messageId))
                properties.MessageId = guid;
            else
                properties.MessageId = messageId;

            if (string.IsNullOrEmpty(Exchange))
                channel.BasicPublish(Exchange, QueueOrTopicName, properties, body);
            else
                channel.BasicPublish(Exchange, routingKey, properties, body);
        }
        
        private void WriteToExchange<T>(IModel channel, T body, string messageId, string routingKey, IDictionary<string, object> headers)
        {
            byte[] encodedMessage = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));

            var properties = channel.CreateBasicProperties();

            string guid = System.Guid.NewGuid().ToString();

            properties.Headers = headers;

            properties.DeliveryMode = 2; //persistent

            if (string.IsNullOrEmpty(messageId))
                properties.MessageId = guid;
            else
                properties.MessageId = messageId;

            if (string.IsNullOrEmpty(Exchange))
                channel.BasicPublish(Exchange, QueueOrTopicName, properties, encodedMessage);
            else
                channel.BasicPublish(Exchange, routingKey, properties, encodedMessage);

            Console.WriteLine($"Message was published on exchange {Exchange} at {DateTime.Now}");
        }

        private void ConnectRabbitMQ()
        {
            if (rabbitConnection == null || !rabbitConnection.IsOpen)
            {
                lock (lockRabbitConnection)
                {
                    if (rabbitConnection == null || !rabbitConnection.IsOpen)
                    {
                        var factory = parameters.SearchConnectionFactory();

                        rabbitConnection = factory.CreateConnection();
                    }
                }
            }
        }
    }
}