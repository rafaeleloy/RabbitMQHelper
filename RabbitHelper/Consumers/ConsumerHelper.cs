using Newtonsoft.Json;
using NLog;
using RabbitHelper.Connector;
using RabbitHelper.Logs;
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
        private readonly Logger _logControl;

        private readonly Func<IConnection> ConnectionFactory;
        private EventingBasicConsumer Consumer;
        private readonly QueueParametersGeneric Parameters;

        private static IModel Channel;

        private bool SuccessConsume = false;
        private string QueueOrTopicName { get; set; }
        private string RoutingKey { get; set; }
        private List<string> TopicName { get; set; }
        private ushort PrefetchCount { get; set; }
        private bool IsExclusive { get; set; }
        private Dictionary<ulong, int> ReSend { get; set; }

        public ConsumerHelper(ILogControl logControl, string queueNameOrTopicName, List<string> topicName = null,
                              ushort prefetchCount = 100, bool isExclusive = false, string routingKey = "")
        {
            _logControl = logControl.GetLogger(GetType().Name);

            Parameters = new QueueParametersGeneric();
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
        /// <param name="withParameterType">If the received message wold be send with parameter type of the method 'Consume'.</param>
        public void RegisterConsumers()
        {
            EnsureHasConsumer();

            foreach (var method in GetConsumerMethods())
            {
                var parameter = method.GetParameters().FirstOrDefault();

                Consumer.Received += ConsumerThread;

                _logControl.Info($"Starting consume => {QueueOrTopicName}");
            }
        }

        private void ConsumerThread(object sender, BasicDeliverEventArgs e)
        {
            try
            {
                var thread = new Thread(() => ConsumerReceived(sender, e));
                thread.Start();
            }
            catch(ThreadStartException ex)
            {
                _logControl.Error($"Error while trying to start the consumer thread, error: {ex.Message}");
            }
            catch(Exception ex)
            {
                _logControl.Error($"Error while trying to start the consumer thread, error: {ex.Message}");
            }
        }

        private IEnumerable<MethodInfo> GetConsumerMethods()
        {
            return Assembly
                   .GetEntryAssembly()
                   .GetTypes()
                   .Where(mytype => mytype.GetInterfaces().Contains(typeof(IConsumer)) && mytype.IsClass)
                   .SelectMany(x => x.GetMethods().Where(x => x.Name == "Consume"));
        }

        private void ConsumerReceived(object sender, BasicDeliverEventArgs e)
        {
            foreach (var method in GetConsumerMethods())
            {
                var parameter = method.GetParameters();

                var message = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(e.Body), parameter.FirstOrDefault().ParameterType);
                bool containsHeader = parameter.Select(parameterDictionary => parameterDictionary.ParameterType == typeof(Dictionary<string, string>)).ToList().Count > 0;

                List<object> parameterList = new List<object>();
                parameterList.Add(message);

                if (containsHeader)
                {
                    if (e.BasicProperties.Headers != null)
                        parameterList.Add(e.BasicProperties.Headers);
                }

                SuccessConsume = (bool)method.Invoke(method.DeclaringType.Assembly.CreateInstance(method.DeclaringType.FullName), parameterList.ToArray());

                if (SuccessConsume)
                {
                    Channel.BasicAck(e.DeliveryTag, false);

                    _logControl.Info($"Consumed message => {e.DeliveryTag} at {DateTime.Now}");
                }
                else
                {
                    ulong deliveryTag = e.DeliveryTag;
                    ReSend.TryGetValue(deliveryTag, out int tries);

                    if (!ReSend.ContainsKey(deliveryTag))
                    {
                        ReSend.Add(deliveryTag, 1);
                        Channel.BasicReject(deliveryTag, true);

                        _logControl.Warn($"Error on consume message => {deliveryTag} message was requeued");
                    }
                    else if (tries < 4)
                    {
                        ReSend[deliveryTag] = tries++;
                        Channel.BasicReject(deliveryTag, true);

                        _logControl.Warn($"Error on consume message => {deliveryTag} message was requeued");
                    }
                    else
                    {
                        Channel.BasicReject(deliveryTag, false);

                        _logControl.Warn($"Error on consume message => {deliveryTag} message was moved to delay queue");
                    }
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

                Channel = ConnectionFactory().CreateModel();
                Channel.BasicQos(0, PrefetchCount, false);

                // Declara a fila para garantir que ela existe.
                if (TopicName != null)
                    QueueHelper.DeclareTopicQueue(Channel, QueueOrTopicName, TopicName, RoutingKey);
                else if (IsExclusive)
                    QueueHelper.DeclareExclusiveQueue(Channel, QueueOrTopicName);
                else
                    QueueHelper.DeclareQueue(Channel, QueueOrTopicName, IsExclusive);

                Consumer = new EventingBasicConsumer(Channel);
                Channel.BasicConsume(QueueOrTopicName, false, Consumer);

                _logControl.Info("Establishing connection with RabbitMQ");
            }
        }
    }
}
