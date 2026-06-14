using System.Net.Sockets;

namespace NetLab.Protocol.Exchange
{
    public class StreamExchange : IExchange
    {
        private readonly NetworkStream _stream;
        private readonly Socket _socket;

        public StreamExchange(NetworkStream stream)
        {
            _stream = stream;
            _socket = stream.Socket;
        }

        public bool IsConnected
        {
            get
            {
                try
                {
                    return !(_socket.Poll(0, SelectMode.SelectRead) && _socket.Available == 0);
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Send(byte[] data)
        {
            _stream.Write(data, 0, data.Length);
            _stream.Flush();
        }
        //基于 TCP 已经保证的“有序字节流”自己循环 Read直到凑够 count 个字节
        public byte[]? ReadExactly(int count)
        {
            byte[] buffer = new byte[count];
            int total = 0;
            while (total < count)
            {
                int n = _stream.Read(buffer, total, count - total);
                //没数据但连接未断时，Read 会等；n == 0 基本表示连接关闭
                if (n == 0) return null;
                total += n;
            }
            return buffer;
        }

        public void Close()
        {
            try { _stream.Close(); } catch { }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
