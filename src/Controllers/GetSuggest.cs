using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hl18
{
    [Flags]
    public enum SuggestQueryMask
    {
        Sex = 1 << 0,
        Country = 1 << 1,
        City = 1 << 2,
    }

    public class GetSuggest: ICtxProcessor
    {
        private readonly Storage store;
        public GetSuggest(Storage storage)
        {
            store = storage;
        }

        // synchronously process the request, fill up responseBuffer, and return statusCode
        public int Process(HttpCtx ctx, int id)
        {
            var startTime = Stats.Watch.Elapsed;
            var limit = 0;
            if (!Mapper.ExtIdToIntId(id, out id))
                return 404; // no mapping found

            var acct = store.Accounts[id];
            if( acct.IsEmpty() )
                return 404; // no such user

            var flags = new SuggestQueryMask();
            var cityIdx = 0;
            var countryIdx = 0;
            bool empty = false;

            foreach (var query in ctx.Params)
            {
                var value = query.Value;
                if (value.IsEmpty)
                    return 400;
                if (query.Key == "query_id")
                { } // ignore
                else
                if (query.Key == "limit")
                {
                    if (!value.TryToInt(out limit))
                        return 400;
                }
                else
                if (query.Key == "country" )
                {
                    if (value.IsEmpty)
                        return 400;
                    if (store.Countries.TryGetValue(value, out IRange countryInd))
                        countryIdx = countryInd.Index;
                    else
                        empty = true;
                    flags |= SuggestQueryMask.Country;
                }
                else
                if (query.Key == "city")
                {
                    if (value.IsEmpty)
                        return 400;
                    if (store.Cities.TryGetValue(value, out var cityBm))
                        cityIdx = cityBm.Index;
                    else
                        empty = true;
                    flags |= SuggestQueryMask.City;
                }
                else // all other parameters are invalid
                    return 400;
            }

            if( limit<=0 )
                return 400;

            if (empty || acct.LikesCount == 0) // shortcut
                return 211; // empty accounts

            // first, create the list of suggesters and find their similarity value
            var acctLikes = acct.GetLikes(store);
            if (!Pool<List<(int id, double sim)>>.TryGet(out var suggesters))
                suggesters = new List<(int id, double sim)>(4096);

            for (int i = 0; i < acctLikes.Count; i++)
            {
                var q = store.Accounts[acctLikes[i].GetId()];
                foreach (var suggester in q.GetLikedBy(store) )
                {
                    var a = store.Accounts[suggester];
                    if (suggester != id && // can't suggest to myself
                        a.IsMale() == acct.IsMale() && // suggester must be same gender
                        (cityIdx == 0 || cityIdx == a.CityIdx) && // from the specified city
                        (countryIdx == 0 || countryIdx == a.CountryIdx) ) // from the specified country
                    {
#if false
                        long key = id < suggester ? (id + ((long)suggester << 32)) : (suggester + ((long)id << 32));
                        if (!store.CachedSim.TryGetValue(key, out var similarity))
                        {
                            // if it's a new suggester, calculate his similarity
                            similarity = calcSimilarity(acctLikes, a.GetLikes(store));
                            store.CachedSim.Add(key, similarity);
                        }
#else
                        // if it's a new suggester, calculate his similarity
                        var similarity = calcSimilarity(acctLikes, a.GetLikes(store));
                        //store.CachedSim.Add(key, similarity);
#endif
                        if (similarity > 0)
                            suggesters.Add( (suggester, similarity) );
                    }
                }
            }

            // suggesters is sorted by id, now, sort it by descending similarity then by ascending ids
            suggesters.Sort((x, y) => y.sim.CompareTo(x.sim));

            // compose the response
            var sb = new AStringBuilder(ctx.Buffer, ctx.ResponseBodyStart);

            sb.Append("{\"accounts\":[");
            bool firstEl = true;

            // form the list of suggestions
            var suggestions = new List<int>(limit);
            var lastSuggester = 0;
            foreach( var kv in suggesters )
            {
                if (kv.id == lastSuggester)
                    continue;
                else
                    lastSuggester = kv.id;
                var a = store.Accounts[kv.id];
                var aLikes = a.GetLikes(store);
                // now, add suggested likes that were not yet liked by acct
                for (int k = aLikes.Count-1; k >=0 ; k--)
                {
                    var l0 = aLikes[k];
                    if (l0.GetId() == id)
                        continue;
                    var j1 = Array.BinarySearch(store.Likes, acct.LikesIdx, acct.LikesCount, l0, Like.CompareById);
                    if (j1 < 0) // suggest only not already liked accounts
                        if (!suggestions.Contains(l0.GetId()))
                        {
                            suggestions.Add(l0.GetId());
                            outputAccount(l0.GetId(), sb, ref firstEl);
                            if (suggestions.Count == limit)
                                break;
                        }
                }
                if (suggestions.Count >= limit)
                    break;
            }

            suggesters.Clear();
            Pool<List<(int, double)>>.Release(suggesters);

            // finalize the output
            sb.Append("]}");
            ctx.ResponseBodyLength = sb.Count;

            var stopTime = Stats.Watch.Elapsed;
            ctx.ContextType = "GetSuggest";
            return 200;
        }

        private void outputAccount(int i, AStringBuilder sb, ref bool firstEl)
        {
            var a = store.Accounts[i];
            if (firstEl)
                firstEl = false;
            else
                sb.Append(',');

            // id (always present)
            sb.Append("{\"id\":").Append(Mapper.IntIdToExtId(i)).Append(',');

            // email
            sb.Append("\"email\":");
            if (a.Email == null)
                sb.Append("null");
            else
            {
                sb.Append('"');
                store.emailFromBuffer(a.Email, sb);
                sb.Append("\",");
            }

            // fname
            if (a.FNameIdx > 0)
                sb.Append("\"fname\":\"").Append(store.Fnames[a.FNameIdx].AName).Append("\",");

            // sname
            if (a.SNameIdx != 0)
                sb.Append("\"sname\":\"").Append(store.Snames[a.SNameIdx].AName).Append("\",");

            // status
            sb.Append("\"status\":\"");
            if (a.IsFree())
                sb.Append(DtoAccount.s_Free);
            else
            if (a.IsTaken())
                sb.Append(DtoAccount.s_Taken);
            else
                sb.Append(DtoAccount.s_Complicated);
            sb.Append("\"}");
        }

        private static double calcSimilarity(ArraySegment<Like> likes0, ArraySegment<Like> likes1)
        {
            // intersect 2 sorted arrays
            double similarity = 0.0;
            int i = 0, j = 0;
            while (i < likes0.Count && j < likes1.Count)
            {
                var l0 = likes0[i];
                var l1 = likes1[j];
                if (l0.GetId() < l1.GetId())
                    i++;
                else 
                if (l1.GetId() < l0.GetId())
                    j++;
                else /* l0.GetId()==l1.GetId() */
                {
                    if (l0.GetTs() == l1.GetTs())
                        similarity += 1.0;
                    else
                        similarity += 1.0 / Math.Abs(l0.GetTs() - l1.GetTs());
                    i++;
                    j++;
                }
            }
            return similarity;
        }

        class SimilarityComparer : IComparer<KeyValuePair<int, double>>
        {
            public int Compare(KeyValuePair<int, double> x, KeyValuePair<int, double> y)
            {
                return x.Value.CompareTo(y.Value);
            }
            public static SimilarityComparer Instance = new SimilarityComparer();
        }

        class IdComparer : IComparer<KeyValuePair<int, double>>
        {
            public int Compare(KeyValuePair<int, double> x, KeyValuePair<int, double> y)
            {
                return x.Key.CompareTo(y.Key);
            }
            public static IdComparer Instance = new IdComparer();
        }
    }
}
