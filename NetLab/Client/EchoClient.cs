using System.Net.Sockets;
using System.Text;
using NetLab.Protocol;
using NetLab.Protocol.Channels;
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
                Console.WriteLine("已发送 BinaryAsdu：AAAA");

                IAsdu msg2 = new JsonAsdu("{\"cmd\":\"hello\",\"id\":1}");
                FrameCodec.SendFrame(exchange, msg2.GetContent());
                Console.WriteLine("已发送 JsonAsdu：{\"cmd\":\"hello\",\"id\":1}");

                IAsdu msg3 = new BinaryAsdu(new byte[] { 0x01 });
                FrameCodec.SendFrame(exchange, msg3.GetContent());
                Console.WriteLine("已发送 BinaryAsdu：[0x01]");

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
                        Console.WriteLine($"收到回显：{response.Length} 字节 → {Encoding.UTF8.GetString(response)}");
                }

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

        /// <summary>
        /// Phase 4 演示：建三条不同 Business 的连接，每条走不同 Channel。
        /// Net.Connect 不走连接池。
        /// </summary>
        public void RunDispatchTest()
        {
            Net.CreateExchange = (host, port) =>
            {
                TcpClient client = new TcpClient(host, port);
                return new StreamExchange(client.GetStream());
            };

            // 连接 1：Manage → EchoChannel
            Console.WriteLine("--- 连接 1：Manage ---");
            using IExchange? conn1 = Net.Connect(_host, _port, Business.Manage);
            if (conn1 != null)
            {
                IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes("管理消息"));
                FrameCodec.SendFrame(conn1, msg.GetContent());
                byte[]? resp = FrameCodec.ReceiveFrame(conn1);
                if (resp != null)
                    Console.WriteLine($"Manage 回显：{Encoding.UTF8.GetString(resp)}");
            }

            // 连接 2：File → FileChannel（分块传输模拟）
            Console.WriteLine("--- 连接 2：File ---");
            using IExchange? conn2 = Net.Connect(_host, _port, Business.File);
            if (conn2 != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes($"请发第{i}块"));
                    FrameCodec.SendFrame(conn2, msg.GetContent());

                    byte[]? chunk = FrameCodec.ReceiveFrame(conn2);
                    if (chunk == null)
                    {
                        Console.WriteLine("File 连接已关闭");
                        break;
                    }
                    Console.WriteLine($"File 收到分块：{Encoding.UTF8.GetString(chunk)}");
                }
                Console.WriteLine("File 传输完成");
            }

            // 连接 3：Agent → AgentChannel
            Console.WriteLine("--- 连接 3：Agent ---");
            using IExchange? conn3 = Net.Connect(_host, _port, Business.Agent);
            if (conn3 != null)
            {
                IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes("代理数据"));
                FrameCodec.SendFrame(conn3, msg.GetContent());
                Console.WriteLine("Agent 消息已发送");
            }

            Console.WriteLine("========== Phase 4 效果 ==========");
            Console.WriteLine("同一个端口 9000 接受了 3 条不同业务的连接。");
            Console.WriteLine("每条连接通过第一帧协商确定归属（Manage/File/Agent）。");
            Console.WriteLine("Net.Connect 不走连接池——外部业务通道专线专用。");
            Console.ReadKey();
        }

        /// <summary>
        /// Phase 5 演示：两端同时切加密，业务代码一行不改。
        /// </summary>
        public void RunSecureTest()
        {
            // 客户端：一行切换到加密
            Net.CreateExchange = (host, port) =>
            {
                TcpClient client = new TcpClient(host, port);
                return new SecureExchange(new StreamExchange(client.GetStream()));
            };

            using IExchange? conn = Net.Connect(_host, _port, Business.Manage);
            if (conn != null)
            {
                IAsdu msg = new BinaryAsdu(Encoding.UTF8.GetBytes("加密消息"));
                FrameCodec.SendFrame(conn, msg.GetContent());
                byte[]? resp = FrameCodec.ReceiveFrame(conn);
                if (resp != null)
                    Console.WriteLine($"加密通道回显：{Encoding.UTF8.GetString(resp)}");
            }

            Console.WriteLine("========== Phase 5 效果 ==========");
            Console.WriteLine("客户端：SecureExchange(StreamExchange)");
            Console.WriteLine("服务端：ExchangeWrapper = SecureExchange");
            Console.WriteLine("两端的 CreateExchange / ExchangeWrapper 各加一层包装。");
            Console.WriteLine("Net.Connect / FrameCodec / EchoChannel 一行没改。");
            Console.ReadKey();
        }
    }
}
