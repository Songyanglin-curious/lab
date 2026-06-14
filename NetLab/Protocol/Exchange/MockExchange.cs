using NetLab.Protocol.Exchange;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Protocol.Exchange
{
    public class MockExchange : IExchange
    {
        private readonly byte[] _response;
        public MockExchange(byte[] response) { _response = response; }

        public void Send(byte[] data) { /* 不真发 */ }
        public byte[]? ReadExactly(int count) => _response;
        public void Close() { }
        public void Dispose() { }
        public bool IsConnected => true;
    }
}
