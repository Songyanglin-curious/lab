using NetLab.Protocol.Exchange;

namespace Protocol.Channels
{
    public interface IChannel : IDisposable
    {
        void ResponseNegotiate(IExchange exchange);

        void Run(IExchange exchange);
    }
}
