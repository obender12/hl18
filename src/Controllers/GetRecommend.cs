using System;
using System.Collections.Generic;
using System.Linq;

namespace hl18
{
    [Flags]
    public enum RecommendQueryMask
    {
        Sex = 1 << 0,
        Country = 1 << 1,
        City = 1 << 2,
    }


    class FindContext
    {
        public FindContext( Storage store, Finder finder, Account acct, int limit )
        {
            this.store = store;
            this.finder = finder;
            this.acct = acct;
            this.limit = limit;
        }
        Storage store;
        Finder finder;
        Account acct;
        int limit;
        int category;
        long lastKey;
        
        public SortedDictionary<long, int> Selected = new SortedDictionary<long, int>();

        private BitMap selectBaseBitmap()
        {
            switch( category )
            {
                case 0: return acct.IsMale() ? store.Female : store.Male;
                case 1: return acct.IsMale() ? store.PremiumFreeFemale : store.PremiumFreeMale; 
                case 2: return acct.IsMale() ? store.PremiumComplicatedFemale : store.PremiumComplicatedMale; 
                case 3: return acct.IsMale() ? store.PremiumTakenFemale : store.PremiumTakenMale; 
                case 4: return acct.IsMale() ? store.NonPremiumFreeFemale : store.NonPremiumFreeMale; 
                case 5: return acct.IsMale() ? store.NonPremiumComplicatedFemale : store.NonPremiumComplicatedMale; 
                case 6: return acct.IsMale() ? store.NonPremiumTakenFemale : store.NonPremiumTakenMale;
                default: return null;
            }
        }

        public int Select(int category)
        {
            this.category = category;
            lastKey = 0;
            finder.SetBitmap(0, selectBaseBitmap());
            finder.Prepare();
            finder.Find(int.MaxValue, found);
            return Selected.Count;
        }

        public bool found(int id)
        {
            var a = store.Accounts[id];

            var key = (long)category << 60
                | (long)(127 - BitMap96.Common(acct.InterestMask, a.InterestMask)) << 53
                | (long)(Math.Abs(acct.Birth - a.Birth)) << 21
                | (long)(id & ((1 << 21) - 1));
            if (Selected.Count < limit)
            {
                Selected.Add(key, id);
            }
            else
            {
                if (lastKey == 0)
                    lastKey = Selected.Keys.Last();

                if (key < lastKey)
                {
                    Selected.Add(key, id);
                    Selected.Remove(lastKey);
                    lastKey = Selected.Keys.Last();
                }
            }
            return true;
        }
    }

    public class GetRecommend: ICtxProcessor
    {
        private readonly Storage store;
        public GetRecommend(Storage storage)
        {
            store = storage;
        }

        // synchronously process the request, fill up responseBuffer, and return statusCode
        public int Process(HttpCtx ctx, int id)
        {
            var limit = 0;
            if (!Mapper.ExtIdToIntId(id, out id))
                return 404; // no mapping exists

            var finder = new Finder(store.All);
            finder.AndBitmap(store.PremiumFreeMale); // placeholder, to be replaced for each category

            var flags = new RecommendQueryMask();
            var startTime = Stats.Watch.Elapsed;

            foreach (var query in ctx.Params)
            {
                var value = query.Value;
                if (value.IsEmpty)
                    return 400;
                if (query.Key == "query_id")
                { } // ignore
                else
                if (query.Key == "limit" )
                {
                    if (!value.TryToInt(out limit))
                        return 400;
                }
                else
                if (query.Key == "country")
                {
                    if (value.IsEmpty)
                        return 400;
                    if (store.Countries.TryGetValue(value, out IRange countryInd))
                        finder.AndBitmap(countryInd as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= RecommendQueryMask.Country;
                }
                else
                if (query.Key == "city")
                {
                    if (value.IsEmpty)
                        return 400;
                    if (store.Cities.TryGetValue(value, out var cityBm))
                        finder.AndBitmap(cityBm as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= RecommendQueryMask.City;
                }
                else // all other parameters are invalid
                    return 400;
            }

            if ( limit<=0 )
                return 400;

            var acct = store.Accounts[id];
            if (acct.IsEmpty())
                return 404; // no such user
            if (acct.InterestMask.Count == 0)
                return 211; // no interests => zero compatibility
            if (finder.DefaultBitmap==null || acct.InterestMask.Count == 0) // shortcut
                return 211; // empty accounts

#if false
            finder.AddCondition(i => acct.InterestMask.Any(store.Accounts[i].InterestMask), 0);
#else
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (acct.InterestMask.IsSet(i))
                    finder.OrBitmap(0, store.Interests[i] as BitMap);
#endif
            var findContext = new FindContext(store, finder, acct, limit);
            for (int category = 1; category <= 6; category++)
                if (findContext.Select(category) >= limit)
                    break;

            // compose the response
            var sb = new AStringBuilder(ctx.Buffer, ctx.ResponseBodyStart);

            sb.Append("{\"accounts\":[");
            bool firstEl = true;

            // pick first limit users from selected
            foreach (var kv in findContext.Selected)
            {
                var i = kv.Value;
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

                // status
                sb.Append("\"status\":\"");
                if (a.IsFree())
                    sb.Append(DtoAccount.s_Free);
                else
                if (a.IsTaken())
                    sb.Append(DtoAccount.s_Taken);
                else
                    sb.Append(DtoAccount.s_Complicated);
                sb.Append("\",");

                // fname
                if( a.FNameIdx>0 )
                    sb.Append("\"fname\":\"").Append(store.Fnames[a.FNameIdx].AName).Append("\",");

                // sname
                if( a.SNameIdx>0 )
                    sb.Append("\"sname\":\"").Append(store.Snames[a.SNameIdx].AName).Append("\",");

                // premium
                if( store.PremiumYes[i] )
                    sb.Append("\"premium\":{\"start\":").Append(a.PStart).Append(",\"finish\":").Append(a.PFinish).Append("},");

                // birth
                sb.Append("\"birth\":").Append(a.Birth).Append('}');
            };

            // finalize the output
            sb.Append("]}");
            ctx.ResponseBodyLength = sb.Count;

            var stopTime = Stats.Watch.Elapsed;
            ctx.ContextType = "GetRecommend";
            return 200;
        }

    }
}
