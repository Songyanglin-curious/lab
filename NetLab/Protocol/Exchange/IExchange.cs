namespace NetLab.Protocol.Exchange
{
    public interface IExchange : IDisposable
    {
        void Send(byte[] data);

        byte[]? ReadExactly(int count);

        void Close();

        bool IsConnected { get; }
    }
}
