using Newtonsoft.Json;
using RabbitHelper.Connector;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RabbitHelper.Consumers
{
    public class ConsumerHelper
    {
        private readonly QueueParametersGeneric parameters;
        private static readonly Dictionary<string, bool> hasQueue = new Dictionary<string, bool>(5000);

        private static IConnection rabbitConnection;
        private static readonly object lockRabbitConnection = new object();

        private string QueueOrTopicName { get; set; }

        public ConsumerHelper(string queueNameOrTopicName)
        {
            parameters = new QueueParametersGeneric();
            QueueOrTopicName = queueNameOrTopicName;
        }

        /// <summary>
        /// This method Register the method 'Consume' in the class to consume the queue.
        /// </summary>
        /// <param name="channel">The channel connection of RabbitMQ.</param>
        /// <param name="queue">The queue name.</param>
        /// <param name="autoAck">The option autoAck.</param>
        /// <param name="withParameterType">If the received message wold be send with parameter type of the method 'Consume'.</param>
        public void RegisterConsumers(bool autoAck, bool withParameterType)
        {
            ConnectRabbitMQ();

            using (var channel = rabbitConnection.CreateModel())
            {
                foreach (var method in GetConsumerMethods())
                {
                    var parameter = method.GetParameters().FirstOrDefault();

                    var basicConsumer = new EventingBasicConsumer(channel);

                    basicConsumer.Received += ConsumerReceived;

                    channel.BasicConsume(QueueOrTopicName, autoAck, basicConsumer);
                }
            }
        }

        // TO-DO: Confirm the return type of Consume method is bool and check the parameter method to send the corret value
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

                bool successConsume = false;

                if (containsHeader)
                {
                    parameterList.Add(e.BasicProperties.Headers);
                    successConsume = (bool)method.Invoke(method.DeclaringType.Assembly.CreateInstance(method.DeclaringType.FullName), parameterList.ToArray());
                }
                else
                {
                    successConsume = (bool)method.Invoke(method.DeclaringType.Assembly.CreateInstance(method.DeclaringType.FullName), parameterList.ToArray());
                }
            }
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
