using Newtonsoft.Json;
using RabbitHelper.Connector;
using RabbitHelper.Queues;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RabbitHelper.Consumers
{
    public abstract class ConsumerHelper
    {
        private EventingBasicConsumer Consumer;
        public QueueParametersGeneric Parameters;
        private readonly AutoResetEvent _eventHandler = new AutoResetEvent(false);

        private static IModel Channel;

        private bool SuccessConsume = false;
        private string QueueOrTopicName { get; set; }
        private string RoutingKey { get; set; }
        private List<string> TopicName { get; set; }
        private ushort PrefetchCount { get; set; }
        private bool IsExclusive { get; set; }
        private Dictionary<ulong, int> ReSend { get; set; }

        protected virtual bool ProcessMessage(string body, IDictionary<string, object> filters) => true;

        public ConsumerHelper(string queueNameOrTopicName, QueueParametersGeneric parameters = null, List<string> topicName = null,
                              ushort prefetchCount = 100, bool isExclusive = false, string routingKey = "")
        {
            Parameters = parameters;
            QueueOrTopicName = queueNameOrTopicName;
            TopicName = topicName;
            PrefetchCount = prefetchCount;
            IsExclusive = isExclusive;
            RoutingKey = routingKey;
        }

        /// <summary>
        /// This method Register the method 'Consume' in the class to consume the queue.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queue">The queue name.</param>
        /// <param name="autoAck">The option autoAck.</param>
        /// <param name="withParameterType">If the received message would be send with parameter type of the method 'Consume'.</param>
        public void RegisterConsumers()
        {
            if (Channel != null && Channel.IsOpen)
                throw new Exception("There's an already connection opened.");

            EnsureHasConsumer();

            Consumer.Received += ConsumerReceived;

            _eventHandler.WaitOne();

            while (Channel.IsClosed)
                _eventHandler.Set();
        }

        private void ConsumerReceived(object sender, BasicDeliverEventArgs e)
        {
            var message = Encoding.UTF8.GetString(e.Body.ToArray());

            SuccessConsume = ProcessMessage(message, e.BasicProperties.Headers);

            if (SuccessConsume)
            {
                Channel.BasicAck(e.DeliveryTag, false);

                //_logControl.Info($"Consumed message => {e.DeliveryTag} at {DateTime.Now}");
                Console.WriteLine($"Consumed message => {e.DeliveryTag} at {DateTime.Now}");
            }
            else
            {
                ulong deliveryTag = e.DeliveryTag;
                ReSend.TryGetValue(deliveryTag, out int tries);

                if (!ReSend.ContainsKey(deliveryTag))
                {
                    ReSend.Add(deliveryTag, 1);
                    Channel.BasicReject(deliveryTag, true);

                    //_logControl.Warn($"Error on consume message => {deliveryTag} message was requeued");
                    Console.WriteLine($"Error on consume message => {deliveryTag} message was requeued");
                }
                else if (tries < 4)
                {
                    ReSend[deliveryTag] = tries++;
                    Channel.BasicReject(deliveryTag, true);

                    //_logControl.Warn($"Error on consume message => {deliveryTag} message was requeued");
                    Console.WriteLine($"Error on consume message => {deliveryTag} message was requeued");
                }
                else
                {
                    Channel.BasicReject(deliveryTag, false);

                    //_logControl.Warn($"Error on consume message => {deliveryTag} message was moved to delay queue");
                    Console.WriteLine($"Error on consume message => {deliveryTag} message was moved to delay queue");
                }
            }
        }

        private void EnsureHasConsumer()
        {
            if (Consumer == null || !Consumer.IsRunning || !Consumer.Model.IsOpen)
            {
                if (Channel != null)
                {
                    Channel.Close();
                }

                var _factory = Parameters?.SearchConnectionFactory() ?? new QueueParametersGeneric().SearchConnectionFactory();
                var _connection = _factory.CreateConnection();

                Channel = _connection.CreateModel();
                Channel.BasicQos(0, PrefetchCount, false);

                if (TopicName != null)
                    QueueHelper.DeclareTopicQueue(Channel, QueueOrTopicName, TopicName, RoutingKey);
                else if (IsExclusive)
                    QueueHelper.DeclareExclusiveQueue(Channel, QueueOrTopicName);
                else
                    QueueHelper.DeclareQueue(Channel, QueueOrTopicName, IsExclusive);

                Consumer = new EventingBasicConsumer(Channel);
                Channel.BasicConsume(QueueOrTopicName, false, Consumer);

                Console.WriteLine("Establishing connection with RabbitMQ");
            }
        }
    }
}
