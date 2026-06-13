using System.Net.Sockets;
using System.Text;
using NetLab.Client.Messages;

namespace NetLab.Client
{
    public class EchoClient
    {
        private readonly string _host;
        private readonly int _port;

        public EchoClient(string host = "127.0.0.1", int port = 9000)
        {
            _host = host;
            _port = port;
        }

        public void RunTest()
        {
            TcpClient client = new TcpClient(_host, _port);
            Console.WriteLine("已连接到 Echo 服务端");

            using NetworkStream stream = client.GetStream();

            // 业务层构造 ASDU → 转字节 → FrameCodec 只负责收发 byte[]
            IAsdu msg1 = new BinaryAsdu(Encoding.UTF8.GetBytes("AAAA"));
            FrameCodec.SendFrame(stream, msg1.GetContent());
            Console.WriteLine("已发送 BinaryAsdu：AAAA");

            IAsdu msg2 = new JsonAsdu("{\"cmd\":\"hello\",\"id\":1}");
            FrameCodec.SendFrame(stream, msg2.GetContent());
            Console.WriteLine("已发送 JsonAsdu：{\"cmd\":\"hello\",\"id\":1}");

            IAsdu msg3 = new BinaryAsdu(new byte[] { 0x01 });
            FrameCodec.SendFrame(stream, msg3.GetContent());
            Console.WriteLine("已发送 BinaryAsdu：[0x01]");

            // 接收回显
            for (int i = 0; i < 3; i++)
            {
                byte[]? response = FrameCodec.ReceiveFrame(stream);
                if (response != null)
                {
                    string text = Encoding.UTF8.GetString(response);
                    Console.WriteLine($"收到回显帧：{response.Length} 字节 → {text}");
                }
            }

            Console.WriteLine("========== 观察结果 ==========");
            Console.WriteLine("业务层用 IAsdu 对象描述要发什么。");
            Console.WriteLine("传输层(APDU/FrameCodec)负责组帧拆帧。");
            Console.WriteLine("两层各司其职——这就是 ASDU/APDU 分离。");

            client.Close();
            Console.WriteLine("连接已关闭");
            Console.ReadKey();
        }
    }
}
