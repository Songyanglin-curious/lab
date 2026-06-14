namespace NetLab.Protocol.Exchange
{
    /// <summary>
    /// 加密通道包装器（教学模拟）。
    /// 架构位置类比 RemoteOps 的 TLCPDataExchange.TLCPClient，
    /// 加密实现用 XOR 替代 SM4，不模拟 TLCP 握手。
    /// </summary>
    public class SecureExchange : IExchange
    {
        private readonly IExchange _inner;

        public SecureExchange(IExchange inner)
        {
            _inner = inner;
        }

        public bool IsConnected => _inner.IsConnected;

        public void Send(byte[] data)
        {
            byte[] encrypted = Xor(data);
            _inner.Send(encrypted);
        }

        public byte[]? ReadExactly(int count)
        {
            byte[]? encrypted = _inner.ReadExactly(count);
            if (encrypted == null) return null;
            return Xor(encrypted);
        }

        public void Close() => _inner.Close();

        public void Dispose() => _inner.Dispose();

        private static byte[] Xor(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ 0x55);
            return result;
        }
    }
}
