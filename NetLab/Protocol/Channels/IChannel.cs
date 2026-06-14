using NetLab.Protocol.Exchange;

namespace NetLab.Protocol.Channels
{
    public interface IChannel : IDisposable
    {
        void ResponseNegotiate(IExchange exchange);

        void Run(IExchange exchange);
    }
}
