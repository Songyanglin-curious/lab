using System.Net;
using System.Net.Sockets;
using System.Text;
using NetLab.Protocol;
using NetLab.Protocol.Exchange;
using NetLab.Protocol.Messages;

namespace NetLab.Server
{
    public class EchoServer
    {
        private readonly int _port;

        public EchoServer(int port = 9000)
        {
            _port = port;
        }

        public void Start()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            Console.WriteLine($"Echo 服务端已启动，监听 0.0.0.0:{_port}");
            Console.WriteLine("等待客户端连接...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"客户端已连接：{client.Client.RemoteEndPoint}");
                Task.Run(() => HandleClient(client));
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
      
                using IExchange exchange = new StreamExchange(stream);

                while (true)
                {
                    byte[]? payload = FrameCodec.ReceiveFrame(exchange);
                    if (payload == null)
                    {
                        Console.WriteLine($"客户端 {client.Client.RemoteEndPoint} 已断开");
                        break;
                    }

                    if (payload.Length > 0 && payload[0] == 0xFF)
                    {
                        Console.WriteLine($"[{client.Client.RemoteEndPoint}] 收到心跳");
                    }
                    else
                    {
                        string text = Encoding.UTF8.GetString(payload);
                        Console.WriteLine($"[{client.Client.RemoteEndPoint}] 收到帧：{payload.Length} 字节 → {text}");

                        IAsdu asdu = new BinaryAsdu(payload);
                        FrameCodec.SendFrame(exchange, asdu.GetContent());
                        Console.WriteLine($"[{client.Client.RemoteEndPoint}] 回显 {payload.Length} 字节");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理客户端出错：{ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }
    }
}
