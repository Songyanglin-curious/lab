using System.Net.Sockets;
using NetLab.Protocol.Exchange;

namespace NetLab.Protocol
{
    public static class FrameCodec
    {
        private static readonly byte[] Magic = { 0xEB, 0x90 };

        // ===== NetworkStream 版本（保留） =====

        public static void SendFrame(NetworkStream stream, byte[] payload)
        {
            stream.Write(Magic, 0, 2);
            byte[] lenBytes = LengthToBytes(payload.LongLength);
            stream.Write(lenBytes, 0, lenBytes.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        public static byte[]? ReceiveFrame(NetworkStream stream)
        {
            byte[] magic = new byte[2];
            if (ReadExactly(stream, magic, 2) != 2)
                return null;
            if (magic[0] != 0xEB || magic[1] != 0x90)
                throw new Exception($"帧头错误: {magic[0]:X2} {magic[1]:X2}");

            return ReadPayload(stream);
        }

        // ===== IExchange 版本（Phase 2 新增） =====

        public static void SendFrame(IExchange exchange, byte[] payload)
        {
            byte[] lenBytes = LengthToBytes(payload.LongLength);
            byte[] frame = new byte[2 + lenBytes.Length + payload.Length];
            Array.Copy(Magic, 0, frame, 0, 2);
            Array.Copy(lenBytes, 0, frame, 2, lenBytes.Length);
            Array.Copy(payload, 0, frame, 2 + lenBytes.Length, payload.Length);
            exchange.Send(frame);
        }

        public static byte[]? ReceiveFrame(IExchange exchange)
        {
            byte[]? magic = exchange.ReadExactly(2);
            if (magic == null) return null;
            if (magic[0] != 0xEB || magic[1] != 0x90)
                throw new Exception($"帧头错误: {magic[0]:X2} {magic[1]:X2}");

            return ReadPayload(exchange);
        }

        // ===== 读载荷（内部，NetworkStream / IExchange 各一份） =====

        private static byte[]? ReadPayload(NetworkStream stream)
        {
            var lenList = new List<byte>();
            while (true)
            {
                byte[] b = new byte[1];
                if (ReadExactly(stream, b, 1) != 1) return null;
                lenList.Add(b[0]);
                if ((b[0] & 0x80) == 0) break;
            }
            long length = BytesToLength(lenList.ToArray());
            if (length == 0) return Array.Empty<byte>();
            byte[] payload = new byte[length];
            if (ReadExactly(stream, payload, (int)length) != length) return null;
            return payload;
        }

        private static byte[]? ReadPayload(IExchange exchange)
        {
            var lenList = new List<byte>();
            while (true)
            {
                byte[]? b = exchange.ReadExactly(1);
                if (b == null) return null;
                lenList.Add(b[0]);
                if ((b[0] & 0x80) == 0) break;
            }
            long length = BytesToLength(lenList.ToArray());
            if (length == 0) return Array.Empty<byte>();
            if (length > int.MaxValue) throw new Exception($"载荷过大: {length}");
            return exchange.ReadExactly((int)length);
        }

        // ===== 工具 =====

        private static int ReadExactly(NetworkStream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n == 0) return total;
                total += n;
            }
            return total;
        }

        public static byte[] LengthToBytes(long length)
        {
            if (length == 0) return new byte[] { 0x00 };
            var bytes = new List<byte>();
            while (length > 0)
            {
                bytes.Add((byte)(length & 0x7F));
                length >>= 7;
            }
            bytes.Reverse();
            for (int i = 0; i < bytes.Count - 1; i++)
                bytes[i] |= 0x80;
            return bytes.ToArray();
        }

        public static long BytesToLength(byte[] bytes)
        {
            long n = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                n <<= 7;
                n |= (long)(bytes[i] & 0x7F);
            }
            return n;
        }
    }
}
