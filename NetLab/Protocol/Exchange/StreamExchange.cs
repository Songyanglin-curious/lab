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
        
        public byte[]? ReadExactly(int count)
        {
            byte[] buffer = new byte[count];
            int total = 0;
            while (total < count)
            {
                int n;
                try
                {
                    n = _stream.Read(buffer, total, count - total);
                }
                catch (IOException io)
                {
                    Console.WriteLine("IO异常:",io);
                    return null;
                }
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
