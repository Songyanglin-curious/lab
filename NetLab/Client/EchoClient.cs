using System.Net.Sockets;
using System.Text;
using NetLab.Protocol;
using NetLab.Protocol.Exchange;
using NetLab.Protocol.Messages;

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
            try
            {
                Net.CreateExchange = (host, port) =>
                {
                    TcpClient client = new TcpClient(host, port);
                    return new StreamExchange(client.GetStream());
                };

                using IExchange exchange = Net.CreateExchange(_host, _port);
                Console.WriteLine("已连接到 Echo 服务端");
              

                IAsdu msg1 = new BinaryAsdu(Encoding.UTF8.GetBytes("AAAA"));
                FrameCodec.SendFrame(exchange, msg1.GetContent());
                byte[]? response1 = Net.Send(_host, _port, msg1);
                Console.WriteLine("已发送 BinaryAsdu：AAAA");
                if (response1 != null)
                    Console.WriteLine($"收到回显帧：{response1.Length} 字节 → {Encoding.UTF8.GetString(response1)}");
                
                IAsdu msg2 = new JsonAsdu("{\"cmd\":\"hello\",\"id\":1}");
                byte[]? response2 = Net.Send(_host, _port, msg2);
                Console.WriteLine("已发送 JsonAsdu：{\"cmd\":\"hello\",\"id\":1}");
                if (response2 != null)
                    Console.WriteLine($"收到回显帧：{response2.Length} 字节 → {Encoding.UTF8.GetString(response2)}");


                IAsdu msg3 = new BinaryAsdu(new byte[] { 0x01 });
                byte[]? response3 = Net.Send(_host, _port, msg3);
                Console.WriteLine("已发送 BinaryAsdu：[0x01]");
                if (response3 != null)
                    Console.WriteLine($"收到回显帧：{response3.Length} 字节 → {Encoding.UTF8.GetString(response3)}");

                IAsdu heartbeat = new HeartbeatAsdu();
                FrameCodec.SendFrame(exchange, heartbeat.GetContent());
                Console.WriteLine("已发送 HeartbeatAsdu");

                string text5000 = new string('A', 5000);
                IAsdu msg5 = new BinaryAsdu(Encoding.UTF8.GetBytes(text5000));
                FrameCodec.SendFrame(exchange, msg5.GetContent());
                Console.WriteLine($"已发送 {msg5.GetContent().Length} 字节");

                for (int i = 0; i < 4; i++)
                {
                    byte[]? response = FrameCodec.ReceiveFrame(exchange);
                    if (response != null)
                    {
                        string text = Encoding.UTF8.GetString(response);
                        Console.WriteLine($"收到回显帧：{response.Length} 字节 → {text}");
                    }
                }

                Console.WriteLine("========== Phase 2~3 效果 ==========");
                Console.WriteLine("IExchange 抽象了网络读写。");
                Console.WriteLine("Net.CreateExchange 委托决定连接方式。");
                Console.WriteLine("Net.Send 内部使用连接池（ConnectionPool）。");

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("客户端异常：");
                Console.WriteLine(ex);
                Console.ReadKey();
            }
        }

        public void RunPooledTest()
        {
            Net.CreateExchange = (host, port) =>
            {
                TcpClient client = new TcpClient(host, port);
                return new StreamExchange(client.GetStream());
            };

            for (int i = 0; i < 10; i++)
            {
                IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes($"Msg{i}"));
                byte[]? response = Net.Send(_host, _port, msg);
                Console.WriteLine($"请求 {i}：{(response != null ? Encoding.UTF8.GetString(response) : "无响应")}");
            }

            Console.WriteLine("10 次 Net.Send 完成——连接池复用，TCP 连接数远小于 10。");
            Console.ReadKey();
        }
    }
}
