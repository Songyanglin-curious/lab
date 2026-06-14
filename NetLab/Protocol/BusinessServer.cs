using System.Net;
using System.Net.Sockets;
using NetLab.Protocol.Exchange;
using Protocol.Channels;

namespace NetLab.Protocol
{
    public class BusinessServer
    {
        private readonly int _port;
        private readonly Dictionary<Business, Func<IChannel>> _factories = new();

        public BusinessServer(int port)
        {
            _port = port;
        }

        public void RegisterProcess(Business business, Func<IChannel> factory)
        {
            _factories[business] = factory;
        }

        public void Start()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();
            Console.WriteLine($"BusinessServer 已启动，监听 0.0.0.0:{_port}");
            Console.WriteLine($"已注册业务：{string.Join(", ", _factories.Keys)}");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"新连接：{client.Client.RemoteEndPoint}");
                Task.Run(() => HandleConnection(client));
            }
        }

        private void HandleConnection(TcpClient client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                using IExchange exchange = new StreamExchange(stream);
                StartupChannel(exchange);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接处理出错：{ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private void StartupChannel(IExchange exchange)
        {
            byte[]? data = FrameCodec.ReceiveFrame(exchange);
            if (data == null || data.Length == 0)
            {
                Console.WriteLine("协商帧为空，关闭连接");
                exchange.Close();
                return;
            }

            Business business = (Business)data[0];

            if (!_factories.TryGetValue(business, out var factory))
            {
                Console.WriteLine($"未知业务类型：{business}，关闭连接");
                FrameCodec.SendFrame(exchange, new byte[] { 0xFF });
                exchange.Close();
                return;
            }

            Console.WriteLine($"协商成功：{business}");

            using IChannel channel = factory();
            channel.ResponseNegotiate(exchange);
            channel.Run(exchange);
        }
    }
}
