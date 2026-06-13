using System.Text;

namespace NetLab.Server.Messages
{
    public class JsonAsdu : IAsdu
    {
        public string Json { get; }

        public JsonAsdu(string json)
        {
            Json = json;
        }

        public byte[] GetContent() => Encoding.UTF8.GetBytes(Json);
    }
}
