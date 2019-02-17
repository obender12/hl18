using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace hl18
{
    [Flags]
    public enum GroupQueryMask
    {
        KeyInterest = 1 << 0,
        KeyCity = 1 << 1,
        KeyCountry = 1 << 2,
        KeyStatus = 1 << 3,
        KeySex = 1 << 4,

        Interests = 1 << 5,
        City = 1 << 6,
        Country = 1 << 7,
        Status = 1 << 8,
        Sex = 1 << 9,

        Birth = 1 << 10,
        Joined = 1 << 11,
        Likes = 1 << 12,
    }

    public class GetGroup: ICtxProcessor
    {
        private readonly Storage store;
        public GetGroup(Storage storage)
        {
            store = storage;
        }


        [Flags]
        enum Keys
        {
            None = 0,
            Sex = 1 << 0,
            Status = 1 << 1,
            Interests = 1 << 2,
            Country = 1 << 3,
            City = 1 << 4
        }


        public struct GroupItem
        {
            public Storage store; // 8 bytes

            public byte sex; // 1..2
            public byte status; // 1..3
            public short city; // 0, 1..640
            public byte country; // 0, 1..71
            public byte interest; // 0, 1..91

            // strings we need for sorting
            public string Sex => sex == 1 ? Storage.s_Male : Storage.s_Female;
            public string Status => status == 1 ? Storage.s_Free : (status == 2 ? Storage.s_Taken : Storage.s_Complicated);
            public string Country => country == 0 ? string.Empty : store.Countries[country].Name;
            public string City => city == 0 ? string.Empty : store.Cities[city].Name;
            public string Interest => interest == 0 ? string.Empty : store.Interests[interest].Name;

            // astrings we need for output
            public AString ASex => sex == 1 ? DtoAccount.s_Male : DtoAccount.s_Female;
            public AString AStatus => status == 1 ? DtoAccount.s_Free : (status == 2 ? DtoAccount.s_Taken : DtoAccount.s_Complicated);
            public AString ACountry => country == 0 ? AString.Empty : store.Countries[country].AName;
            public AString ACity => city == 0 ? AString.Empty : store.Cities[city].AName;
            public AString AInterest => interest == 0 ? AString.Empty : store.Interests[interest].AName;

            // finally the actual counter
            public int Count;
        }

        public static Dictionary<AString, List<GroupItem>> CachedResults = new Dictionary<AString, List<GroupItem>>();

        // synchronously process the request, fill up responseBuffer, and return statusCode
        public int Process(HttpCtx ctx, int dummy)
        {
            // first step: check if the cache already has the calculated result for this set of parameters
            var cacheKey = ctx.Params.GetParamKey();
            if( CachedResults.TryGetValue(cacheKey, out var cachedList) )
            {
                // bingo! already calculated before
                composeResponse(ctx, cachedList, ctx.Params.Limit);
                return 200;
            }

            var limit = 0;
            var order = 0;
            Keys keyMask = new Keys();
            var keys = new List<Keys>();
            var queryMask = new GroupQueryMask();
            bool empty = false;
            ArraySegment<int> likers = null;

            int locationFrom=-1, locationTo=-1;
            int statusFrom=-1, statusTo=-1;
            int sexFrom=-1, sexTo=-1;
            int yearFrom=-1, yearTo=-1;
            int interestFrom=-1, interestTo=-1;



            foreach (var query in ctx.Params)
            {
                var value = query.Value;
                if( value.IsEmpty )
                    return 400;
                if (query.Key == "query_id")
                { } // ignore
                else
                if (query.Key == "limit")
                {
                    if (!value.TryToInt(out limit))
                        return 400;
                    if (limit <= 0 || limit > 50)
                        return 400;
                }
                else
                if (query.Key == "keys")
                {
                    foreach (var key in value.Split(','))
                    {
                        var bit = Keys.None;
                        if (key == "sex")
                        {
                            bit = Keys.Sex;
                            keys.Add(Keys.Sex);
                            queryMask |= GroupQueryMask.KeySex;
                        }
                        else
                        if (key == "status")
                        {
                            bit = Keys.Status;
                            keys.Add(Keys.Status);
                            queryMask |= GroupQueryMask.KeyStatus;
                        }
                        else
                        if (key == "interests")
                        {
                            bit = Keys.Interests;
                            keys.Add(Keys.Interests);
                            queryMask |= GroupQueryMask.KeyInterest;
                        }
                        else
                        if (key == "country")
                        {
                            bit = Keys.Country;
                            keys.Add(Keys.Country);
                            queryMask |= GroupQueryMask.KeyCountry;
                        }
                        else
                        if (key == "city")
                        {
                            bit = Keys.City;
                            keys.Add(Keys.City);
                            queryMask |= GroupQueryMask.KeyCity;
                        }
                        if (bit == Keys.None)
                            return 400;
                        else
                            keyMask |= bit;
                    }
                    if (keyMask == Keys.None)
                        return 400;
                }
                else
                if( query.Key == "order" )
                {
                    if (!value.TryToInt(out order))
                        return 400;
                    if (order != 1 && order != -1)
                        return 400;
                }
                else
                if (query.Key == "sex" )
                {
                    if (value == "m")
                        sexFrom = sexTo = 1;
                    else
                    if (value == "f")
                        sexFrom = sexTo = 2;
                    else
                        return 400;
                    queryMask |= GroupQueryMask.Sex;
                }
                else
                if (query.Key == "status")
                {
                    if (value==DtoAccount.s_Free)
                        statusFrom = statusTo = 1;
                    else
                    if (value == DtoAccount.s_Taken)
                        statusFrom = statusTo = 2;
                    else
                    if (value == DtoAccount.s_Complicated)
                        statusFrom = statusTo = 3;
                    else
                        return 400;
                    queryMask |= GroupQueryMask.Status;
                }
                else
                if (query.Key == "country")
                {
                    if (store.Countries.TryGetValue(value, out IRange countryInd))
                        locationFrom = locationTo = countryInd.Index;
                    else
                        empty = true;
                    queryMask |= GroupQueryMask.Country;
                }
                else
                if (query.Key == "city")
                {
                    if (store.Cities.TryGetValue(value, out IRange cityInd))
                        locationFrom = locationTo = cityInd.Index;
                    else
                        empty = true;
                    queryMask |= GroupQueryMask.City;
                }
                else
                if (query.Key == "interests")
                {
                    if (store.Interests.TryGetValue(value, out IRange interestRange))
                        interestFrom = interestTo = interestRange.Index;
                    else
                        empty = true;
                    queryMask |= GroupQueryMask.Interests;
                }
                else
                if (query.Key == "likes")
                {
                    if (!value.TryToInt(out int extId))
                        return 400;
                    if (!Mapper.ExtIdToIntId(extId, out int id))
                        return 404;
                    var acct = store.Accounts[id];
                    if (acct.LikedByIdx == 0 || acct.LikedByCount == 0)
                        empty = true;
                    else
                        likers = new ArraySegment<int>(store.LikedBy, acct.LikedByIdx, acct.LikedByCount);
                    queryMask |= GroupQueryMask.Likes;
                }
                else
                if (query.Key == "birth")
                {
                    if (value.TryToInt(out int birthYear))
                    {
                        if (store.BirthYears.TryGetValue(birthYear, out IRange yearMap))
                            yearFrom = yearTo = yearMap.Index;
                        else
                            empty = true;
                    }
                    else
                        return 400;
                    queryMask |= GroupQueryMask.Birth;
                }
                else
                if (query.Key == "joined")
                {
                    if (value.TryToInt(out int joinYear))
                    {
                        if (store.JoinYears.TryGetValue(joinYear, out IRange yearMap))
                            yearFrom = yearTo = yearMap.Index;
                        else
                            empty = true;
                    }
                    else
                        return 400;
                    queryMask |= GroupQueryMask.Joined;
                }
                else // all other parameters are invalid
                    return 400;
            }

            if( limit==0 || order==0)
                return 400;

            if (empty) // shortcut, no groups will be found
                return 212; // empty groups

            // extend the from/to ranges based on the keys
            {
                if (keyMask.HasFlag(Keys.City) && locationFrom < 0)
                {
                    locationFrom = 0;
                    locationTo = store.Cities.Count;
                }
                if (keyMask.HasFlag(Keys.Country) && locationFrom < 0)
                {
                    locationFrom = 0;
                    locationTo = store.Countries.Count;
                }
                if (keyMask.HasFlag(Keys.Sex) && sexFrom < 0)
                {
                    sexFrom = 1;
                    sexTo = 2;
                }
                if (keyMask.HasFlag(Keys.Status) && statusFrom < 0)
                {
                    statusFrom = 1;
                    statusTo = 3;
                }
                if (keyMask.HasFlag(Keys.Interests) && interestFrom < 0)
                {
                    interestFrom = 1;
                    interestTo = store.Interests.Count - 1;
                }
            }

            // find the correct hypercube out of 4, or create new one for likes
            IHypercube cube = null;
            CubeKind cubeKind = CubeKind.None;
            if( keyMask.HasFlag(Keys.City) || queryMask.HasFlag(GroupQueryMask.City) )
            {
                if (queryMask.HasFlag(GroupQueryMask.Birth))
                {
                    if (queryMask.HasFlag(GroupQueryMask.Likes))
                        cubeKind = CubeKind.CityBirth;
                    else
                        cube = store.CubeCityBirth;
                }
                else
                {
                    if (queryMask.HasFlag(GroupQueryMask.Likes))
                        cubeKind = CubeKind.CityJoined;
                    else
                        cube = store.CubeCityJoined;
                }
            }
            else
            {
                if (queryMask.HasFlag(GroupQueryMask.Birth))
                {
                    if (queryMask.HasFlag(GroupQueryMask.Likes))
                        cubeKind = CubeKind.CountryBirth;
                    else
                        cube = store.CubeCountryBirth;
                }
                else
                {
                    if (queryMask.HasFlag(GroupQueryMask.Likes))
                        cubeKind = CubeKind.CountryJoined;
                    else
                        cube = store.CubeCountryJoined;
                }
            }

            HypercubeHash cubeHash = null;
            // for likes, create a custom hypercube and fill it up
            if( cube==null && likers != null )
            {
                if (!Pool<HypercubeHash>.TryGet(out cubeHash))
                    cubeHash = new HypercubeHash(cubeKind, 20000);
                else
                    cubeHash.Reset(cubeKind);
                cube = cubeHash;
                foreach( var id in likers )
                {
                    var acct = store.Accounts[id];
                    int statusIdx = (acct.Flags >> 1) & 3;
                    int sexIdx = (acct.Flags & Account.Male) > 0 ? 1 : 2;
                    int locationIdx = 0;
                    int yearIdx = 0;
                    switch ( cubeKind )
                    {
                        case CubeKind.CityBirth:
                            locationIdx = acct.CityIdx;
                            yearIdx = acct.BirthIdx;
                            break;
                        case CubeKind.CityJoined:
                            locationIdx = acct.CityIdx;
                            yearIdx = acct.JoinedIdx;
                            break;
                        case CubeKind.CountryBirth:
                            locationIdx = acct.CountryIdx;
                            yearIdx = acct.BirthIdx;
                            break;
                        case CubeKind.CountryJoined:
                            locationIdx = acct.CountryIdx;
                            yearIdx = acct.JoinedIdx;
                            break;
                        default:
                            throw new Exception("Unexpected CubeKind");
                    }

                    cubeHash.Include(locationIdx, statusIdx, sexIdx, yearIdx, acct.InterestMask);
                }
            }
            else
            {
                cubeKind = cube.Kind;
            }

            // calculate the size of the group
            var size =
                (locationTo - locationFrom + 1) *
                (statusTo - statusFrom + 1) *
                (sexTo - sexFrom + 1) *
                (yearTo - yearFrom + 1) *
                (interestTo - interestFrom + 1);

            var groupList = new List<GroupItem>(size);

            cube.Slice(
                locationFrom, locationTo,
                statusFrom, statusTo,
                sexFrom, sexTo,
                yearFrom, yearTo,
                interestFrom, interestTo,
                visit);

            // local function, called by the cube
            void visit(int location, int status, int sex, int year, int interest, int count)
            {
                var gi = new GroupItem { store = store, Count = count };
                if (keyMask.HasFlag(Keys.Interests))
                    gi.interest = (byte)interest;
                if (keyMask.HasFlag(Keys.Status))
                    gi.status = (byte)status;
                if (keyMask.HasFlag(Keys.Sex))
                    gi.sex = (byte)sex;
                if (keyMask.HasFlag(Keys.City))
                    gi.city = (short)location;
                if (keyMask.HasFlag(Keys.Country))
                    gi.country = (byte)location;
                groupList.Add(gi);
            }

            if (groupList.Count == 0)
            {
                if (cubeHash != null)
                    Pool<HypercubeHash>.Release(cubeHash);
                CachedResults.TryAdd(cacheKey, groupList);
                return 212;
            }

            var groupComparer = new GroupComparer(order, keys);
            groupList.Sort(groupComparer);
            composeResponse(ctx, groupList, limit);

            // add to the cache for later reuse
            if( groupList.Count>50 )
                groupList.RemoveRange(50, groupList.Count - 50);
            CachedResults.TryAdd(cacheKey, groupList);

            // clean up
            if (cubeHash != null)
                Pool<HypercubeHash>.Release(cubeHash);

            ctx.ContextType = "GetGroup";
            return 200;
        }

        private void composeResponse(HttpCtx ctx, List<GroupItem> groupList, int limit)
        {
            // find and compose the response
            var sb = new AStringBuilder(ctx.Buffer, ctx.ResponseBodyStart);

            sb.Append("{\"groups\":[");
            bool firstGroup = true;

            foreach (var g in groupList)
            {
                if (firstGroup)
                    firstGroup = false;
                else
                    sb.Append(',');

                // count - always there
                sb.Append("{\"count\":").Append(g.Count);
                // sex
                if (g.sex > 0)
                    sb.Append(",\"sex\":\"").Append(g.ASex).Append('"');
                if (g.status > 0)
                    sb.Append(",\"status\":\"").Append(g.AStatus).Append('"');
                if (g.city > 0)
                    sb.Append(",\"city\":\"").Append(g.ACity).Append('"');
                if (g.country > 0)
                    sb.Append(",\"country\":\"").Append(g.ACountry).Append('"');
                if (g.interest > 0)
                    sb.Append(",\"interests\":\"").Append(g.AInterest).Append('"');
                sb.Append('}');

                if (--limit == 0)
                    break; // enough
            }
            sb.Append("]}");
            ctx.ResponseBodyLength = sb.Count;
        }

        class GroupComparer : IComparer<GroupItem>
        {
            readonly int order;
            readonly List<Keys> keys;
            public GroupComparer(int order, List<Keys> keys )
            {
                this.order = order;
                this.keys = keys;
            }

            static IComparer<string> strCmp = new CultureInfo("ru-RU").CompareInfo.GetStringComparer(
                CompareOptions.Ordinal);

            public int Compare(GroupItem x, GroupItem y)
            {
                // first compare by count
                var cmp = (x.Count * order).CompareTo(y.Count * order);
                if (cmp != 0)
                    return cmp;

                for (int i = 0; i < keys.Count; i++)
                    switch (keys[i])
                    {
                        case Keys.Sex:
                            if (order > 0)
                                cmp = strCmp.Compare(x.Sex, y.Sex);
                            else
                                cmp = strCmp.Compare(y.Sex, x.Sex);
                            if (cmp != 0)
                                return cmp;
                            break;
                        case Keys.Status:
                            if (order > 0)
                                cmp = strCmp.Compare(x.Status, y.Status);
                            else
                                cmp = strCmp.Compare(y.Status, x.Status);
                            if (cmp != 0)
                                return cmp;
                            break;
                        case Keys.Interests:
                            if (order > 0)
                                cmp = strCmp.Compare(x.Interest, y.Interest);
                            else
                                cmp = strCmp.Compare(y.Interest, x.Interest);
                            if (cmp != 0)
                                return cmp;
                            break;
                        case Keys.Country:
                            if (order > 0)
                                cmp = strCmp.Compare(x.Country, y.Country);
                            else
                                cmp = strCmp.Compare(y.Country, x.Country);
                            if (cmp != 0)
                                return cmp;
                            break;
                        case Keys.City:
                            if (order > 0)
                                cmp = strCmp.Compare(x.City, y.City);
                            else
                                cmp = strCmp.Compare(y.City, x.City);
                            if (cmp != 0)
                                return cmp;
                            break;
                    }

                return 0;
            }
        }

    }

}
