namespace NetLab.Protocol.Messages
{
    public class HeartbeatAsdu : IAsdu
    {
        public byte[] GetContent()
        {
            return new byte[] { 0xFF };  // 心跳标记
        }
    }
}
