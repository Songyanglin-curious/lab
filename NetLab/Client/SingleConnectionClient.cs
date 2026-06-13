using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetLab.Client
{
    public class SingleConnectionClient
    {
        public void Run()
        {
            // 1. 连到 127.0.0.1:9000
            TcpClient client = new TcpClient("127.0.0.1", 9000);
            Console.WriteLine("已连接到服务器");

            // 2. 拿到网络流
            NetworkStream stream = client.GetStream();

            // 3. 发送请求
            string request = "GetChannelStatus";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            stream.Write(requestBytes, 0, requestBytes.Length);
            Console.WriteLine($"已发送：{request}");

            // 4. 读响应
            byte[] buffer = new byte[1024];
            int readCount = stream.Read(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, readCount);
            Console.WriteLine($"收到响应：{response}");

            // 5. 关闭
            stream.Close();
            client.Close();
            Console.ReadKey();
        }
    }
}
