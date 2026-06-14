using System.Text;
using NetLab.Protocol.Exchange;

namespace NetLab.Protocol.Channels
{
    public class FileChannel : IChannel
    {
        private int _remainingChunks = 4;

        public void ResponseNegotiate(IExchange exchange)
        {
            FrameCodec.SendFrame(exchange, new byte[] { 0x00 });
        }

        public void Run(IExchange exchange)
        {
            Console.WriteLine("  FileChannel 已就绪（分块传输模拟）");
            Console.WriteLine($"  模拟文件大小：{_remainingChunks} 块");

            while (true)
            {
                byte[]? request = FrameCodec.ReceiveFrame(exchange);
                if (request == null)
                {
                    Console.WriteLine("  FileChannel 客户端断开");
                    break;
                }

                string reqText = Encoding.UTF8.GetString(request);
                Console.WriteLine($"  FileChannel 收到分块请求：{reqText}");

                _remainingChunks--;

                if (_remainingChunks > 0)
                {
                    string chunk = $"CHUNK_DATA_{_remainingChunks}";
                    FrameCodec.SendFrame(exchange, Encoding.UTF8.GetBytes(chunk));
                    Console.WriteLine($"  FileChannel 发送分块：{chunk}（剩余 {_remainingChunks} 块）");
                }
                else
                {
                    FrameCodec.SendFrame(exchange, Encoding.UTF8.GetBytes("TRANSFER_COMPLETE"));
                    Console.WriteLine("  FileChannel 所有分块传输完成，关闭连接");
                    break;
                }
            }
        }

        public void Dispose() { }
    }
}
