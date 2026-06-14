using NetLab.Protocol.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Protocol.Messages
{
    public class HeartbeatAsdu : IAsdu
    {
        public byte[] GetContent()
        {
            return new byte[] { 0xFF };  // 心跳标记
        }
    }
}
