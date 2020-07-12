using NLog;

namespace RabbitHelper.Logs
{
    public interface ILogControl
    {
        Logger GetLogger(string name);
    }
}
