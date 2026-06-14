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

        /// <summary>
        /// 发送一条 ASDU 并等响应。走连接池，协商固定为 Business.Equip。
        /// 对应 RemoteOps: InternalSend → UseClient
        /// </summary>
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

        /// <summary>
        /// 创建指定业务的连接。不走连接池——每条外部业务连接专线专用。
        /// 对应 RemoteOps: BaseMainServer.Connect()
        /// 区别于 Net.Send（走池，固定 Equip 协商）。
        /// </summary>
        public static IExchange? Connect(string ip, int port, Business business)
        {
            if (CreateExchange == null)
                throw new InvalidOperationException("CreateExchange 未注册。");

            IExchange exchange = CreateExchange(ip, port);

            // 发协商帧：首字节 = Business 类型
            FrameCodec.SendFrame(exchange, new byte[] { (byte)business });

            // 等协商回应：[0x00]=成功, 其他=失败
            byte[]? response = FrameCodec.ReceiveFrame(exchange);
            if (response == null || response.Length == 0 || response[0] != 0x00)
            {
                Console.WriteLine($"协商失败：{business}，响应={(response != null ? response[0].ToString("X2") : "null")}");
                exchange.Dispose();
                return null;
            }

            Console.WriteLine($"协商成功：{business}");
            return exchange;
        }
    }
}
