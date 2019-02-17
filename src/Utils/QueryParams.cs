using System.Collections.Generic;

namespace hl18
{
    public class QueryParams : Dictionary<AString, AString>
    {
        public QueryParams(int capacity)
            : base(capacity)
        {
        }

        // special parameters
        public int Limit = 0;
        public int QueryId = 0;

        private AString s_limit = new AString("limit");
        private AString s_queryId = new AString("query_id");

        public new void Add(AString key, AString value)
        {
            if (key == s_limit)
                value.TryToInt(out Limit);
            else
            if (key == s_queryId)
                value.TryToInt(out QueryId);

            if (!TryAdd(key, value))
                this[key] += new AString(",") + value;
        }

        public void Add(string key, int value)
        {
            Add(key, value.ToString());
        }

        public override string ToString()
        {
            AString rval = new AString();
            foreach (var kv in this)
                if (rval.IsEmpty)
                    rval = kv.Key + new AString("=") + kv.Value;
                else
                    rval += "&" + kv.Key + "=" + kv.Value;
            return rval.ToString();
        }

        public AString GetParamKey()
        {
            // compose the cache key from params
            var paramlist = new List<KeyValuePair<AString, AString>>(this);
            paramlist.RemoveAll(x => x.Key == "query_id" || x.Key == "limit");
            paramlist.Sort((x, y) => x.Key.CompareTo(y.Key));
            var paramSize = 0;
            foreach (var p in paramlist)
                paramSize += p.Key.Length + p.Value.Length;
            var buf = new byte[paramSize];
            paramSize = 0;
            foreach (var p in paramlist)
            {
                for (int i = 0; i < p.Key.Length; i++)
                    buf[paramSize++] = p.Key[i];
                for (int i = 0; i < p.Value.Length; i++)
                    buf[paramSize++] = p.Value[i];
            }
            return new AString(buf);
        }
    }
}
