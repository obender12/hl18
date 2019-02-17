using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hl18
{
    [Flags]
    public enum FilterQueryMask
    {
        Sex_eq = 1 << 0,
        Email_domain = 1 << 1,
        Email_ltgt = 1 << 2,
        Status_eq = 1 << 3, 
        Fname_eq = 1 << 4,
        Fname_any = 1 << 5,
        Fname_null = 1 << 6,
        Sname_eq = 1 << 7,
        Sname_starts = 1 << 8,
        Sname_null = 1 << 9,
        Phone_code = 1 << 10,
        Phone_null = 1 << 11,
        Country_eq = 1 << 12,
        Country_null = 1 << 13,
        City_eq = 1 << 14,
        City_any = 1 << 15,
        City_null = 1 << 16,
        Birth_ltgt = 1 << 17,
        Birth_year = 1 << 18,
        Interests_all = 1 << 19,
        Interests_any = 1 << 20,
        Likes_contains = 1 << 21,
        Premium_now = 1 << 22,
        Premium_null = 1 << 23,
    }

    public class GetFilter: ICtxProcessor
    {
        private readonly Storage store;

        public static Dictionary<AString, List<int>> CachedResults = new Dictionary<AString, List<int>>();

        public GetFilter(Storage storage)
        {
            store = storage;
        }

        // synchronously process the request, fill up responseBuffer, and return statusCode
        public int Process(HttpCtx ctx, int dummy)
        {
            var limit = 0;
            var finder = new Finder( store.All );
            HashSet<int> likers = null;
            var orGroup = 0;
            var flags = new FilterQueryMask();

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
                if (query.Key == "sex_eq")
                {
                    if (value == "m")
                        finder.AndBitmap(store.Male);
                    else
                    if (value == "f")
                        finder.AndBitmap(store.Female);
                    else
                        return 400;
                    flags |= FilterQueryMask.Sex_eq;
                }
                else
                if (query.Key == "email_domain")
                {
                    if (store.Domains.TryGetValue(value, out var domainMap))
                        finder.AndBitmap(domainMap as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Email_domain;
                }
                else
                if (query.Key == "email_lt")
                {
                    var ltHash = emailHash(value) + 1;
                    finder.AddCondition(i => store.Accounts[i].GetEmailHash() < ltHash, 2);
                    flags |= FilterQueryMask.Email_ltgt;
                }
                else
                if (query.Key == "email_gt")
                {
                    var gtHash = emailHash(value);
                    finder.AddCondition(i => store.Accounts[i].GetEmailHash() > gtHash, 2);
                    flags |= FilterQueryMask.Email_ltgt;
                }
                else
                if (query.Key == "status_eq" || query.Key == "status_neq")
                {
                    bool statusYes = query.Key == "status_eq";
                    BitMap statusMap;
                    if (value == DtoAccount.s_Free)
                        statusMap = statusYes ? store.Free : store.NotFree;
                    else
                    if (value == DtoAccount.s_Taken)
                        statusMap = statusYes ? store.Taken : store.NotTaken;
                    else
                    if (value == DtoAccount.s_Complicated)
                        statusMap = statusYes ? store.Complicated : store.NotComplicated;
                    else
                        return 400;
                    finder.AndBitmap(statusMap);
                    flags |= FilterQueryMask.Status_eq;
                }
                else
                if (query.Key == "fname_eq")
                {
                    if (store.Fnames.TryGetValue(value, out IRange fnameMap))
                        finder.AndBitmap(fnameMap as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Fname_eq;
                }
                else
                if (query.Key == "fname_any")
                {
                    var fnames = value.Split(',');
                    foreach (var s in fnames)
                    {
                        if (store.Fnames.TryGetValue(s, out IRange fnameOneMap))
                            finder.OrBitmap(orGroup, fnameOneMap as BitMap);
                    }
                    if (fnames.Length > 0)
                        orGroup++;
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Fname_any;
                }
                else
                if (query.Key == "fname_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.FnameYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.FnameNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.Fname_null;
                }
                else
                if (query.Key == "sname_eq")
                {
                    if (store.Snames2.TryGetValue(value[0] + (value[1] << 16), out var snameInd))
                        finder.AndBitmap(snameInd as BitMap);
                    else
                        finder.AndBitmap(null);
                    finder.AddCondition(i => store.Snames[store.Accounts[i].SNameIdx].Name == value, 2);
                    flags |= FilterQueryMask.Sname_eq;
                }
                else
                if (query.Key == "sname_starts")
                {
                    int snameKey = value[0] + (value[1] << 8);
                    if (value.Length > 2)
                        snameKey += (value[2] << 16) + (value[3] << 24);
                    if (store.Snames2.TryGetValue(snameKey, out var snameInd))
                        finder.AndBitmap(snameInd as BitMap);
                    else
                        finder.AndBitmap(null);
                    finder.AddCondition(i => {
                        return store.Accounts[i].SNameIdx > 0 &&
                        store.Snames[store.Accounts[i].SNameIdx].AName.StartsWith(value);
                    }, 2);
                    flags |= FilterQueryMask.Sname_starts;
                }
                else
                if (query.Key == "sname_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.SnameYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.SnameNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.Sname_null;
                }
                else
                if (query.Key == "phone_code")
                {
                    if (store.AreaCodes.TryGetValue(value, out IRange areaInd))
                        finder.AndBitmap(areaInd as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Phone_code;
                }
                else
                if (query.Key == "phone_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.PhoneYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.PhoneNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.Phone_null;
                }
                else
                if (query.Key == "country_eq")
                {
                    if (store.Countries.TryGetValue(value, out IRange countryInd))
                        finder.AndBitmap(countryInd as BitMap);
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Country_eq;
                }
                else
                if (query.Key == "country_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.CountryYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.CountryNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.Country_null;
                }
                else
                if (query.Key == "city_eq")
                {
                    if (store.Cities.TryGetValue(value, out var cityBm))
                        finder.AndBitmap(cityBm as BitMap);
                    else
                        finder.AndBitmap(null);

                    flags |= FilterQueryMask.City_eq;
                }
                else
                if (query.Key == "city_any")
                {
                    var cities = value.Split(',');
                    foreach (var s in cities)
                        if (store.Cities.TryGetValue(s, out var cityInd))
                            finder.OrBitmap(orGroup, cityInd as BitMap);
                    if (cities.Length > 0)
                        orGroup++;
                    else
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.City_any;
                }
                else
                if (query.Key == "city_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.CityYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.CityNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.City_null;
                }
                else
                if (query.Key == "birth_lt")
                {
                    if (value.TryToInt(out int birthDay))
                        finder.AddCondition(i => store.Accounts[i].Birth < birthDay, 1);
                    else
                        return 400;
                    flags |= FilterQueryMask.Birth_ltgt;
                }
                else
                if (query.Key == "birth_gt")
                {
                    if (value.TryToInt(out int birthDay))
                        finder.AddCondition(i => store.Accounts[i].Birth > birthDay, 1);
                    else
                        return 400;
                    flags |= FilterQueryMask.Birth_ltgt;
                }
                else
                if (query.Key == "birth_year")
                {
                    if (value.TryToInt(out int birthYear))
                    {
                        if (store.BirthYears.TryGetValue(birthYear, out IRange yearMap))
                            finder.AndBitmap(yearMap as BitMap);
                        else
                            finder.AndBitmap(null);
                    }
                    else
                        return 400;
                    flags |= FilterQueryMask.Birth_year;
                }
                else
                if (query.Key == "interests_contains")
                {
                    foreach (var s in value.Split(','))
                        if (store.Interests.TryGetValue(s, out IRange interestRange))
                            finder.AndBitmap(interestRange as BitMap);
                    flags |= FilterQueryMask.Interests_all;
                }
                else
                if (query.Key == "interests_any")
                {
                    var parts = value.Split(',');
                    foreach (var s in parts)
                        if (store.Interests.TryGetValue(s, out IRange interestRange))
                            finder.OrBitmap(orGroup, interestRange as BitMap);
                    if (parts.Length > 0)
                        orGroup++;
                    flags |= FilterQueryMask.Interests_any;
                }
                else
                if (query.Key == "likes_contains")
                {
                    foreach (var s in value.Split(','))
                    {
                        if (!s.TryToInt(out int extId))
                            return 400;
                        if (!Mapper.ExtIdToIntId(extId, out int id))
                        {
                            finder.AndBitmap(null);
                            break;
                        }
                        var acct = store.Accounts[id];
                        if (acct.LikedByCount == 0)
                        {
                            finder.AndBitmap(null);
                            break;
                        }
                        var likedBy = new ArraySegment<int>(store.LikedBy, acct.LikedByIdx, acct.LikedByCount);
                        if (likers == null)
                            likers = new HashSet<int>(likedBy);
                        else
                            likers.IntersectWith(likedBy);
                    }
                    if (likers == null || likers.Count == 0)
                        finder.AndBitmap(null);
                    flags |= FilterQueryMask.Likes_contains;
                }
                else
                if (query.Key == "premium_now")
                {
                    finder.AndBitmap(store.PremiumNow);
                    flags |= FilterQueryMask.Premium_now;
                }
                else
                if (query.Key == "premium_null")
                {
                    if (value == "0")
                        finder.AndBitmap(store.PremiumYes);
                    else
                    if (value == "1")
                        finder.AndBitmap(store.PremiumNo);
                    else
                        return 400;
                    flags |= FilterQueryMask.Premium_null;
                }
                else // all other parameters are invalid
                    return 400;
            }

            if( limit<=0 )
                return 400;


            // shortcut
            if (!finder.Prepare()) 
                return 211;

            // check in the cache first
            var cacheKey = ctx.Params.GetParamKey();
            if (CachedResults.TryGetValue(cacheKey, out var cachedList ))
                if( cachedList.Count>=limit)
                {
                    // bingo! compose the cached list
                    composeResults(ctx, cachedList, flags, limit);
                    return 200;
                }

            var result = new List<int>();
            if (likers == null)
            {
                // all bitmaps and conditions are handled by finder
                finder.Find(limit, id => { result.Add(id); return true; } );
            }
            else
            {
                // create the list for filtered ids 
                foreach (var id in likers)
                    if (finder.Check(id))
                        result.Add(id);
                // sort in the descending order and 
                result.Sort((x, y) => y.CompareTo(x));
                if( result.Count>50 )
                    result.RemoveRange(50, result.Count - 50);
            }

            // serialize into the output buffer
            composeResults(ctx, result, flags, limit);

            // store in cache
            if( !CachedResults.TryAdd(cacheKey, result) ) 
                if (CachedResults.TryGetValue(cacheKey, out var oldCache) && oldCache.Count < result.Count)
                    CachedResults[cacheKey] = result; // this entry has bigger size, replace the old cache

            ctx.ContextType = "GetFilter";
            return 200;
        }

        private int emailHash(AString value)
        {
            if (value.Length == 4) // 2 characters, typical case
                return (byte)value[0] << 24 | (byte)value[1] << 16 | (byte)value[2] << 8 | (byte)value[3];
            if ( value.Length==2) // less typical case
                return (byte)value[0] << 24 | (byte)value[1] << 16;
            if (value.Length == 3) 
                return (byte)value[0] << 24 | (byte)value[1] << 16 | (byte)value[2] << 8;
            if (value.Length == 1)
                return (byte)value[0] << 24;
            return 0;
        }

        private void composeResults(HttpCtx ctx, List<int> ids, FilterQueryMask flags, int limit)
        {
            // find and compose the response
            var sb = new AStringBuilder(ctx.Buffer, ctx.ResponseBodyStart);
            sb.Append("{\"accounts\":[");
            bool firstEl = true;

            foreach( int id in ids)
            { 
                var acct = store.Accounts[id];
                if (firstEl)
                    firstEl = false;
                else
                    sb.Append(',');
                // id (always present)
                sb.Append("{\"id\":").Append(Mapper.IntIdToExtId(id)).Append(',');

                // sex
                if (flags.HasFlag(FilterQueryMask.Sex_eq))
                    sb.Append(store.Male[id] ? "\"sex\":\"m\"," : "\"sex\":\"f\",");

                // status
                if (flags.HasFlag(FilterQueryMask.Status_eq))
                {
                    sb.Append("\"status\":\"");
                    if (store.Free[id])
                        sb.Append(DtoAccount.s_Free);
                    else
                    if (store.Taken[id])
                        sb.Append(DtoAccount.s_Taken);
                    else
                        sb.Append(DtoAccount.s_Complicated);
                    sb.Append("\",");
                }

                // fname
                if (acct.FNameIdx > 0 && (
                    flags.HasFlag(FilterQueryMask.Fname_eq) ||
                    flags.HasFlag(FilterQueryMask.Fname_any) ||
                    flags.HasFlag(FilterQueryMask.Fname_null)))
                    sb.Append("\"fname\":\"").Append(store.Fnames[acct.FNameIdx].AName).Append("\",");

                // sname
                if (acct.SNameIdx != 0 && (
                    flags.HasFlag(FilterQueryMask.Sname_eq) ||
                    flags.HasFlag(FilterQueryMask.Sname_starts) ||
                    flags.HasFlag(FilterQueryMask.Sname_null)))
                    sb.Append("\"sname\":\"").Append(store.Snames[acct.SNameIdx].AName).Append("\",");

                // phone
                if (acct.Phone != null && (
                    flags.HasFlag(FilterQueryMask.Phone_code) ||
                    flags.HasFlag(FilterQueryMask.Phone_null)))
                    sb.Append("\"phone\":\"").Append(acct.Phone).Append("\",");

                // country
                if (acct.CountryIdx > 0 && (
                    flags.HasFlag(FilterQueryMask.Country_eq) ||
                    flags.HasFlag(FilterQueryMask.Country_null)))
                    sb.Append("\"country\":\"").Append(store.Countries[acct.CountryIdx].AName).Append("\",");

                // city
                if (acct.CityIdx > 0 && (
                    flags.HasFlag(FilterQueryMask.City_eq) ||
                    flags.HasFlag(FilterQueryMask.City_any) ||
                    flags.HasFlag(FilterQueryMask.City_null)))
                    sb.Append("\"city\":\"").Append(store.Cities[acct.CityIdx].AName).Append("\",");

                // birth
                if (flags.HasFlag(FilterQueryMask.Birth_ltgt) ||
                    flags.HasFlag(FilterQueryMask.Birth_year))
                    sb.Append("\"birth\":").Append(acct.Birth).Append(',');

                // premium
                if (flags.HasFlag(FilterQueryMask.Premium_now) ||
                    flags.HasFlag(FilterQueryMask.Premium_null))
                    if (acct.PStart != 0 || acct.PFinish != 0)
                        sb.Append("\"premium\":{\"start\":").Append(acct.PStart).Append(",\"finish\":").Append(acct.PFinish).Append("},");

                // email
                sb.Append("\"email\":");
                if (acct.Email == null)
                    sb.Append("null");
                else
                {
                    sb.Append('"');
                    store.emailFromBuffer(acct.Email, sb);
                    sb.Append('"');
                }

                // interests and likes are not required
                sb.Append('}');

                if (--limit <= 0)
                    break;
            }

            sb.Append("]}");
            ctx.ResponseBodyLength = sb.Count;
        }
    }
}
