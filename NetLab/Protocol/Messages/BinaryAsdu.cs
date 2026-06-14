namespace NetLab.Protocol.Messages
{
    public class BinaryAsdu : IAsdu
    {
        public byte[] Data { get; }

        public BinaryAsdu(byte[] data)
        {
            Data = data;
        }

        public byte[] GetContent() => Data;
    }
}
