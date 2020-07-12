using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace RabbitHelper.Logs
{
    public class LogControl : ILogControl
    {
        public static readonly LoggingConfiguration LogConfig;

        static LogControl()
        {
            var cfg = new LoggingConfiguration();

            var jsonLayout = new JsonLayout();
            jsonLayout.Attributes.Add(new JsonAttribute("date", Layout.FromString("${longdate}")));
            jsonLayout.Attributes.Add(new JsonAttribute("level",
                Layout.FromString("${level:upperCase=true}")));
            jsonLayout.Attributes.Add(new JsonAttribute("message",
                Layout.FromString("${message}")));
            jsonLayout.Attributes.Add(new JsonAttribute("file", Layout.FromString("${callsite}:${callsite-linenumber}")));
            jsonLayout.Attributes.Add(new JsonAttribute("exception",
                Layout.FromString("${exception:format=ToString}")));

            var consoleTarget = new ColoredConsoleTarget("target1")
            {
                Layout = jsonLayout
            };

            cfg.AddTarget(consoleTarget);

            cfg.AddRuleForAllLevels(consoleTarget);

            LogManager.Configuration = cfg;
        }

        public Logger GetLogger(string name) => LogManager.GetLogger(name);
    }
}
