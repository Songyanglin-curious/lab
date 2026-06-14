using System.Text;
using NetLab.Protocol.Exchange;
using NetLab.Protocol.Messages;
using Protocol.Channels;

namespace NetLab.Protocol.Channels
{
    public class EchoChannel : IChannel
    {
        public void ResponseNegotiate(IExchange exchange)
        {
            FrameCodec.SendFrame(exchange, new byte[] { 0x00 });
        }

        public void Run(IExchange exchange)
        {
            while (true)
            {
                byte[]? payload = FrameCodec.ReceiveFrame(exchange);
                if (payload == null) break;

                if (payload.Length > 0 && payload[0] == 0xFF)
                {
                    Console.WriteLine($"  EchoChannel 收到心跳");
                    continue;
                }

                string text = Encoding.UTF8.GetString(payload);
                Console.WriteLine($"  EchoChannel 收到：{payload.Length} 字节 → {text}");

                IAsdu asdu = new BinaryAsdu(payload);
                FrameCodec.SendFrame(exchange, asdu.GetContent());
                Console.WriteLine($"  EchoChannel 回显 {payload.Length} 字节");
            }
            Console.WriteLine("  EchoChannel 连接断开");
        }

        public void Dispose() { }
    }
}
