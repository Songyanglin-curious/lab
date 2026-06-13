using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetLab.Server
{
    public class SingleConnectionServer
    {
        public void Run()
        {

            // 1. 创建一个 TcpListener，监听 127.0.0.1:9000
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 9000);
            listener.Start();
            Console.WriteLine("服务器已启动，等待连接...");

            // 2. 阻塞等待客户端连接（有人连上来才继续往下走）
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine($"客户端已连接：{client.Client.RemoteEndPoint}");

            // 3. 拿到网络流（可以像读文件一样读写网络数据）
            NetworkStream stream = client.GetStream();

            // 4. 读客户端发来的消息
            byte[] buffer = new byte[1024];
            int readCount = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, readCount);
            Console.WriteLine($"收到请求：{request}");

            // 5. 处理请求，生成响应
            string response;
            if (request == "GetChannelStatus")
                response = "通道状态：已开启";
            else
                response = "未知请求";

            // 6. 把响应发回去
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            Console.WriteLine($"已回复：{response}");

            // 7. 关闭
            stream.Close();
            client.Close();
            listener.Stop();
            Console.WriteLine("服务器已关闭");
            Console.ReadKey();
        }
    }
}
