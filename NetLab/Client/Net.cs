using NetLab.Protocol;
using NetLab.Protocol.Exchange;
using NetLab.Protocol.Messages;

namespace NetLab.Client
{
    public static class Net
    {
        public delegate IExchange CreateExchangeFunc(string ip, int port);

        public static CreateExchangeFunc? CreateExchange = null;

        private static ConnectionPool? _pool;

        public static byte[]? Send(string ip, int port, IAsdu asdu)
        {
            if (CreateExchange == null)
                throw new InvalidOperationException("CreateExchange 未注册。");

            _pool ??= new ConnectionPool(CreateExchange);

            using ExchangeLease? lease = _pool.UseClient(ip, port);
            if (lease == null) return null;

            FrameCodec.SendFrame(lease.Exchange, asdu.GetContent());
            return FrameCodec.ReceiveFrame(lease.Exchange);
        }
    }
}
