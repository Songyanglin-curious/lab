using NetLab.Protocol.Exchange;

namespace NetLab.Protocol.Channels
{
    public class AgentChannel : IChannel
    {
        public void ResponseNegotiate(IExchange exchange)
        {
            FrameCodec.SendFrame(exchange, new byte[] { 0x00 });
        }

        public void Run(IExchange exchange)
        {
            Console.WriteLine("  AgentChannel 已就绪（桩实现）");
            // 桩：持续收消息直到断开
            while (true)
            {
                byte[]? data = FrameCodec.ReceiveFrame(exchange);
                if (data == null) break;
                Console.WriteLine($"  AgentChannel 收到 {data.Length} 字节");
            }
            Console.WriteLine("  AgentChannel 连接断开");
        }

        public void Dispose() { }
    }
}
