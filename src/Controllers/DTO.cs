using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Utf8Json;

/// <summary>
/// Data Transfer Objects
/// </summary>

namespace hl18
{
    [Flags]
    public enum DtoFlags
    {
        Id = 1<<0,
        Sex = 1<<1,
        Email = 1<<2,
        Status = 1<<3,
        Fname = 1<<4,
        Sname = 1<<5,
        Phone = 1<<6,
        Country = 1<<7,
        City = 1<<8,
        Birth = 1<<9,
        Interests = 1<<10,
        Likes = 1<<11,
        Premium = 1<<12,
        Joined = 1<<13,

        Init = ~0
    }

    public struct DtoPremium
    {
        public int start;
        public int finish;

        private static ArraySegment<byte> s_start = Encoding.ASCII.GetBytes("start");
        private static ArraySegment<byte> s_finish = Encoding.ASCII.GetBytes("finish");

        public static bool Parse(ref JsonReader reader, ref DtoPremium dto)
        {
            if (!reader.ReadIsBeginObject())
                return false;
            var propName = reader.ReadPropertyNameSegmentRaw();
            if (propName.EqualTo(s_start))
                dto.start = reader.ReadInt32();
            else
            if (propName.EqualTo(s_finish) )
                dto.finish = reader.ReadInt32();
            else
                return false;
            if (!reader.ReadIsValueSeparator())
                return false;
            propName = reader.ReadPropertyNameSegmentRaw();
            if (propName.EqualTo(s_start))
                dto.start = reader.ReadInt32();
            else
            if (propName.EqualTo(s_finish))
                dto.finish = reader.ReadInt32();
            else
                return false;
            if (!reader.ReadIsEndObject())
                return false;
            return true;
        }
    }

    public class DtoAccount
    {
        public static int STATUS_FREE = 1;
        public static int STATUS_TAKEN = 2;
        public static int STATUS_COMPLICATED = 3;
        public static bool SEX_MALE = true;
        public static bool SEX_FEMALE = false;

        public int id;
        public short fnameIdx;
        public short snameIdx;
        public AString email;
        public BitMap96 interests;
        public int status;
        public DtoPremium premium;
        public bool sex;
        public AString phone;
        public List<Like> likes = new List<Like>(128);
        public int birth;
        public short cityIdx;
        public byte countryIdx;
        public int joined;
        public DtoFlags flags;

        // clean up before returning to the pool
        public DtoAccount Reset()
        {
            id = birth = joined = premium.start = premium.finish = 0;
            fnameIdx = snameIdx = cityIdx = countryIdx = 0;
            phone = email = AString.Empty;
            status = 0; sex = false;
            interests = new BitMap96();
            likes.Clear();
            flags = 0;
            return this;
        }

        public static ArraySegment<byte> s_Male = Encoding.UTF8.GetBytes("m");
        public static ArraySegment<byte> s_Female = Encoding.UTF8.GetBytes("f");
        public static AString s_Free = new AString(Encoding.UTF8.GetBytes("свободны"));
        public static AString s_Taken = new AString(Encoding.UTF8.GetBytes("заняты"));
        public static AString s_Complicated = new AString(Encoding.UTF8.GetBytes("всё сложно"));
        private static ArraySegment<byte> s_id = Encoding.ASCII.GetBytes("id");
        private static ArraySegment<byte> s_status = Encoding.ASCII.GetBytes("status");
        private static ArraySegment<byte> s_sex = Encoding.ASCII.GetBytes("sex");
        private static ArraySegment<byte> s_email = Encoding.ASCII.GetBytes("email");
        private static ArraySegment<byte> s_phone = Encoding.ASCII.GetBytes("phone");
        private static ArraySegment<byte> s_fname = Encoding.ASCII.GetBytes("fname");
        private static ArraySegment<byte> s_sname = Encoding.ASCII.GetBytes("sname");
        private static ArraySegment<byte> s_country = Encoding.ASCII.GetBytes("country");
        private static ArraySegment<byte> s_city = Encoding.ASCII.GetBytes("city");
        private static ArraySegment<byte> s_birth = Encoding.ASCII.GetBytes("birth");
        private static ArraySegment<byte> s_joined = Encoding.ASCII.GetBytes("joined");
        private static ArraySegment<byte> s_premium = Encoding.ASCII.GetBytes("premium");
        private static ArraySegment<byte> s_interests = Encoding.ASCII.GetBytes("interests");
        private static ArraySegment<byte> s_likes = Encoding.ASCII.GetBytes("likes");
        private static ArraySegment<byte> s_ts = Encoding.ASCII.GetBytes("ts");

        private static ArraySegment<byte> s_male = Encoding.ASCII.GetBytes("m");
        private static ArraySegment<byte> s_female = Encoding.ASCII.GetBytes("f");

        // using low-level to catch wrong structure or properties
        // ids and likes are internalized
        public static bool Parse(ref JsonReader reader, DtoAccount dto, Storage store)
        {
            var count = 0;
            if (reader.ReadIsNull() || !reader.ReadIsBeginObject())
                return false;
            while (true)
            {
                var prop = reader.ReadPropertyNameSegmentRaw();
                // id
                if (prop.EqualTo(s_id))
                {
                    if (reader.GetCurrentJsonToken() != JsonToken.Number)
                        return false;
                    dto.id = Mapper.ExtIdToIntIdCreate(reader.ReadInt32());
                    dto.flags |= DtoFlags.Id;
                } else
                // status
                if( prop.EqualTo(s_status))
                {
                    var st = new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape();
                    if (st==s_Free)
                        dto.status = STATUS_FREE;
                    else
                    if (st==s_Taken)
                        dto.status = STATUS_TAKEN;
                    else
                    if (st==s_Complicated )
                        dto.status = STATUS_COMPLICATED;
                    else
                        return false;
                    dto.flags |= DtoFlags.Status;
                }
                else
                // sex
                if( prop.EqualTo(s_sex))
                {
                    var st = reader.ReadStringSegmentRaw();
                    if (st.EqualTo(s_male))
                        dto.sex = SEX_MALE;
                    else
                    if (st.EqualTo(s_female))
                        dto.sex = SEX_FEMALE;
                    else
                        return false;
                    dto.flags |= DtoFlags.Sex;
                } else
                // email
                if (prop.EqualTo(s_email))
                {
                    dto.email = reader.ReadStringSegmentRaw();
                    dto.flags |= DtoFlags.Email;
                } else
                // phone
                if (prop.EqualTo(s_phone))
                {
                    dto.phone = new AString(reader.ReadStringSegmentRaw()).Duplicate();
                    dto.flags |= DtoFlags.Phone;
                }
                else
                // fname
                if (prop.EqualTo(s_fname))
                {
                    dto.fnameIdx = (short)store.Fnames.GetOrCreateRange(
                        new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape()).Index;
                    dto.flags |= DtoFlags.Fname;
                }
                else
                // sname
                if (prop.EqualTo(s_sname))
                {
                    dto.snameIdx = (short)store.Snames.GetOrCreateRange(
                        new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape()).Index;
                    dto.flags |= DtoFlags.Sname;
                }
                else
                // country
                if (prop.EqualTo(s_country))
                {
                    dto.countryIdx = (byte)store.Countries.GetOrCreateRange(
                        new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape()).Index;
                    dto.flags |= DtoFlags.Country;
                }
                else
                // city
                if (prop.EqualTo(s_city))
                {
                    dto.cityIdx = (short)store.Cities.GetOrCreateRange(
                        new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape()).Index;
                    dto.flags |= DtoFlags.City;
                }
                else
                // birth
                if (prop.EqualTo(s_birth))
                {
                    if (reader.GetCurrentJsonToken() != JsonToken.Number)
                        return false;
                    dto.birth = reader.ReadInt32();
                    dto.flags |= DtoFlags.Birth;
                }
                else
                // joined
                if (prop.EqualTo(s_joined))
                {
                    if (reader.GetCurrentJsonToken() != JsonToken.Number)
                        return false;
                    dto.joined = reader.ReadInt32();
                    dto.flags |= DtoFlags.Joined;
                }
                else
                // premium
                if (prop.EqualTo(s_premium))
                {
                    if (!DtoPremium.Parse(ref reader, ref dto.premium))
                        return false;
                    dto.flags |= DtoFlags.Premium;
                }
                else
                // interests
                if (prop.EqualTo(s_interests))
                {
                    if (!reader.ReadIsBeginArray())
                        return false;
                    while (!reader.ReadIsEndArrayWithSkipValueSeparator(ref count))
                        dto.interests.Set( store.Interests.GetOrCreateRange(new AString(reader.ReadStringSegmentRaw()).InPlaceUnescape()).Index);
                    dto.flags |= DtoFlags.Interests;
                }
                else
                // likes
                if (prop.EqualTo(s_likes))
                {
                    if (!reader.ReadIsBeginArray())
                        return false;

                    // read likes
                    if( reader.GetCurrentJsonToken() == JsonToken.BeginObject)
                        while (true)
                        {
                            Like like = new Like();

                            if (!reader.ReadIsBeginObject())
                                return false;

                            while (true)
                            {
                                var propName = reader.ReadPropertyNameSegmentRaw();
                                if (propName[0] == (byte)'t') // ts 
                                {
                                    if (reader.GetCurrentJsonToken() != JsonToken.Number)
                                        return false;
                                    like.ts = Like.ExtToIntTS(reader.ReadInt32());
                                }
                                else
                                if (propName[0] == (byte)'i') // id
                                {
                                    if (reader.GetCurrentJsonToken() != JsonToken.Number)
                                        return false;
                                    if (!Mapper.ExtIdToIntId(reader.ReadInt32(), out like.id))
                                        return false;
                                }
                                else // not ts nor id
                                    return false;

                                if (!reader.ReadIsValueSeparator())
                                    break;
                            }

                            if (!reader.ReadIsEndObject())
                                return false;

                            dto.likes.Add(like);
                            if (!reader.ReadIsValueSeparator())
                                break;
                        }

                    if (!reader.ReadIsEndArray())
                        return false;
                    dto.flags |= DtoFlags.Likes;
                }
                else
                    return false;

                if (!reader.ReadIsValueSeparator())
                    break;
            }
            if (!reader.ReadIsEndObject())
                return false;

            return true;
        }

        private static ConcurrentBag<DtoAccount> bag = new ConcurrentBag<DtoAccount>();
        public static DtoAccount Obtain()
        {
            if (!bag.TryTake(out var obj))
                obj = new DtoAccount();
            return obj;
        }
        public static void Release(DtoAccount obj)
        {
            obj.Reset();
            bag.Add(obj);
        }

    }

    // collection of accounts, used only for loading
    public class DtoAccounts
    {
        public List<DtoAccount> accounts = new List<DtoAccount>();
    }


    // likes update
    public struct DtoLike
    {
        public int likee;
        public int ts;
        public int liker;
    }

    // collection of likes
    public class DtoLikes
    {
        public List<DtoLike> likes = new List<DtoLike>(128);

        public void Reset()
        {
            likes.Clear();
        }

        private static ConcurrentBag<DtoLikes> bag = new ConcurrentBag<DtoLikes>();
        public static DtoLikes Obtain()
        {
            if (!bag.TryTake(out var obj))
                obj = new DtoLikes();
            return obj;
        }
        public static void Release(DtoLikes obj)
        {
            obj.Reset();
            bag.Add(obj);
        }

        private static ArraySegment<byte> s_likes = Encoding.ASCII.GetBytes("likes");
        private static ArraySegment<byte> s_likee = Encoding.ASCII.GetBytes("likee");
        private static ArraySegment<byte> s_liker = Encoding.ASCII.GetBytes("liker");
        private static ArraySegment<byte> s_ts = Encoding.ASCII.GetBytes("ts");
        private static ArraySegment<byte> s_id = Encoding.ASCII.GetBytes("id");

        public static bool Parse(byte[] buffer, int start, DtoLikes dto, Storage store)
        {
            JsonReader reader = new JsonReader(buffer, start);
            if (reader.ReadIsNull() || !reader.ReadIsBeginObject())
                return false;

            if (!reader.ReadPropertyNameSegmentRaw().EqualTo(s_likes))
                return false;

            // read array members
            if (!reader.ReadIsBeginArray())
                return false;

            while (true)
            {
                var like = new DtoLike();

                // read array of DtoLike objects
                if (!reader.ReadIsBeginObject())
                    break;

                // like properties
                while(true)
                {
                    var prop = reader.ReadPropertyNameSegmentRaw();
                    if (prop[0] == (byte)'t') // ts
                    {
                        if (reader.GetCurrentJsonToken() != JsonToken.Number)
                            return false;
                        like.ts = Like.ExtToIntTS(reader.ReadInt32());
                    }
                    else
                    if (prop[prop.Count - 1] == (byte)'e') // likee
                    {
                        if (reader.GetCurrentJsonToken() != JsonToken.Number)
                            return false;
                        if (!Mapper.ExtIdToIntId(reader.ReadInt32(), out like.likee))
                            return false;
                    }
                    else
                    if (prop[prop.Count - 1] == (byte)'r') // liker
                    {
                        if (reader.GetCurrentJsonToken() != JsonToken.Number)
                            return false;
                        if (!Mapper.ExtIdToIntId(reader.ReadInt32(), out like.liker))
                            return false;
                    }
                    else
                        return false;

                    if (!reader.ReadIsValueSeparator())
                        break;
                }

                if (!reader.ReadIsEndObject())
                    return false;

                // add the like
                if (like.likee > 0 && like.liker > 0)
                    dto.likes.Add(like);
                else
                    return false;

                if (!reader.ReadIsValueSeparator())
                    break;
            }
            if (!reader.ReadIsEndArray())
                return false;
            if (!reader.ReadIsEndObject())
                return false;

            return true;
        }

        public static bool Parse(ref JsonReader reader, ref Like like)
        {
            return true;
        }

    }
}
