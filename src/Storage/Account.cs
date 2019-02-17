using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace hl18
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Account
    {
        public static byte Male = 1;
        public static byte Free = 2;
        public static byte Taken = 4;
        public static byte Premium = 8;
        public static byte LikesDirty = 16;
        public static byte LikedByDirty = 32;

        public byte Flags; // sex, status
        public byte CountryIdx; // index from Countries or 0
        public byte BirthIdx; // index of year of birth
        public byte JoinedIdx; // index of year of joined 

        public short CityIdx; // index from Cities or 0
        public short FNameIdx; // index from FNames or 0

        public short SNameIdx; // index from SNames or 0
        public short Reserved; // reserved for future use

        public int Birth;

        public int PStart;

        public int PFinish;

        public byte[] Email; // always string

        public byte[] Phone; // phone number

        public BitMap96 InterestMask; // struct 12 bytes

        public int LikesIdx; // index in the Likes array

        public int LikedByIdx; // index in the LikedBy array

        public byte LikesCount; // number of used like slots
        public byte LikesCapacity; // number of reserved like slots
        public byte LikedByCount; // number of used likedby slots
        public byte LikedByCapacity; // number of reserved likedby slots

        // helpers
        public byte GetDomainIdx() => Email[Email.Length - 1];
        public int GetEmailHash() => Email[0] << 24 | Email[1] << 16 | Email[2] << 8 | Email[3];
        public bool IsEmpty()=> Flags == 0;
        public bool IsMale() => (Flags & Male) > 0;
        public bool IsFree() => (Flags & Free) > 0 && (Flags & Taken) == 0;
        public bool IsTaken()=> (Flags & Free) == 0 && (Flags & Taken) > 0;
        public bool IsComplicated() => (Flags & Free) > 0 && (Flags & Taken) > 0;
        public bool IsPremium() => (Flags & Premium) > 0;

        public ArraySegment<Like> GetLikes(Storage store) => new ArraySegment<Like>(store.Likes, LikesIdx, LikesCount);
        public ArraySegment<int> GetLikedBy(Storage store) => new ArraySegment<int>(store.LikedBy, LikedByIdx, LikedByCount);
    }
}
