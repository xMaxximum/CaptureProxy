using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public class HeaderCollection : IDisposable
    {
        private Dictionary<string, List<string>> Headers { get; } = new Dictionary<string, List<string>>();

        public long ContentLength
        {
            get
            {
                var tmp = GetFirstValue("Content-Length");
                if (tmp == null) return 0;

                if (long.TryParse(tmp, out var length) == false) return 0;
                return length;
            }
            set
            {
                if (value == 0)
                {
                    Remove("Content-Length");
                    return;
                }
                AddOrReplace("Content-Length", value.ToString());
            }
        }

        public void Dispose()
        {
            foreach (var item in Headers) item.Value.Clear();
            Headers.Clear();
        }

        public void Add(string key, string value)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false)
            {
                Headers[key] = new List<string>();
            }

            Headers[key].Add(value);
        }

        public void AddOrReplace(string key, string value)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false)
            {
                Headers[key] = new List<string>();
            }

            Headers[key].Clear();
            Headers[key].Add(value);
        }

        public void Remove(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return;

            Headers[key].Clear();
            Headers.Remove(key);
        }

        public Dictionary<string, List<string>> GetAll()
        {
            return Headers;
        }

        public List<string> GetValues(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return new List<string>();

            return Headers[key];
        }

        public string? GetFirstValue(string key)
        {
            key = key.ToLower();

            if (Headers.ContainsKey(key) == false) return null;

            return Headers[key].FirstOrDefault();
        }

        public void SetProxyAuthorization(string username, string pass)
        {
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{pass}"));
            AddOrReplace("Proxy-Authorization", $"Basic {credentials}");
        }
    }
}
