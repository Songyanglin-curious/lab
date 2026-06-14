using System.Collections.Concurrent;
using NetLab.Protocol.Exchange;

namespace NetLab.Client
{
    public class ExchangeLease : IDisposable
    {
        public IExchange Exchange { get; }
        public bool IsUsing { get; internal set; }

        public ExchangeLease(IExchange exchange)
        {
            Exchange = exchange;
            IsUsing = true;
        }

        public void Dispose()
        {
            IsUsing = false;
        }
    }

    internal class PoolBucket
    {
        public readonly object Lock = new();
        public readonly List<ExchangeLease> Items = new();
    }

    public class ConnectionPool
    {
        private readonly Net.CreateExchangeFunc _factory;
        private readonly ConcurrentDictionary<string, PoolBucket> _buckets = new();

        public ConnectionPool(Net.CreateExchangeFunc factory)
        {
            _factory = factory;
        }

        public ExchangeLease? UseClient(string ip, int port)
        {
            string key = $"{ip}:{port}";
            PoolBucket bucket = _buckets.GetOrAdd(key, _ => new PoolBucket());

            lock (bucket.Lock)
            {
                for (int i = bucket.Items.Count - 1; i >= 0; i--)
                {
                    ExchangeLease lease = bucket.Items[i];
                    if (lease.IsUsing) continue;

                    if (lease.Exchange.IsConnected)
                    {
                        lease.IsUsing = true;
                        return lease;
                    }

                    lease.Exchange.Close();
                    bucket.Items.RemoveAt(i);
                }

                IExchange exchange = _factory(ip, port);
                if (exchange == null) return null;

                ExchangeLease newLease = new ExchangeLease(exchange);
                bucket.Items.Add(newLease);
                return newLease;
            }
        }
    }
}
