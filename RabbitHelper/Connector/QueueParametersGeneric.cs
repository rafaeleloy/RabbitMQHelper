using RabbitMQ.Client;
using System;

namespace RabbitHelper.Connector
{
    public class QueueParametersGeneric
    {
        public string Server { get; set; }
        public int? Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }

        public int TimeOut
        {
            get
            {
                return 60 * 1000; //thousandth of a second
            }
        }

        public QueueParametersGeneric()
        {
            Server = Environment.GetEnvironmentVariable("RB_SERVER");
            Port = TryGetEnviromentVariable("RB_PORT", 0);
            UserName = Environment.GetEnvironmentVariable("RB_USER");
            Password = Environment.GetEnvironmentVariable("RB_PWD");
            VirtualHost = Environment.GetEnvironmentVariable("RB_VHOST");

            Console.WriteLine("RabbitMQ Config: ");
            Console.WriteLine("   Server     : " + Server);
            Console.WriteLine("   Port       : " + Port);
            Console.WriteLine("   User       : " + UserName);
            Console.WriteLine("   VirtualHost: " + VirtualHost);
        }

        public ConnectionFactory SearchConnectionFactory()
        {
            var connection = new ConnectionFactory()
            {
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            if (!string.IsNullOrEmpty(Server))
            {
                connection.HostName = Server;
            }

            if (Port.HasValue)
            {
                connection.Port = Port.Value;
            }

            connection.VirtualHost = VirtualHost; 

            if (!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password))
            {
                connection.UserName = UserName;
                connection.Password = Password;
            }

            return connection;
        }

        private int TryGetEnviromentVariable(string variable, int defaultValue)
        {
            return Environment.GetEnvironmentVariable(variable) is null ? defaultValue : int.Parse(Environment.GetEnvironmentVariable(variable));
        }
    }
}
