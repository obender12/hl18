using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Utf8Json;

namespace hl18
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Like
    {
        // don't access direcly, use GetId() and GetTs()
        public int id;
        public int ts;

        public Like(int id, int ts)
        {
            this.id = id;
            this.ts = ts;
        }

        public static int TSLikeOffset =
            (int)(new DateTime(2014, 1, 1) - new DateTime(1970, 1, 1)).TotalSeconds;

        // in-place conversion of DTO Like into internal Id/SumTS/Count representation
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ExtToIntTS(int unixTS) => unixTS - TSLikeOffset;

        // account ID is low 21 bits of id (0..2M)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetId() => id & ((1 << 21) - 1);

        // account ID is low 21 bits of id (0..2M)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetTs() => ts / GetNLikes();

        // number of likes is high 11 bits of id (up to 2048 likes)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNLikes() => (id >> 21) + 1;

        // converter this like into an internal like (internal likeId and tranlated timestamp)
        public Like ToInternal()
        {
            return new Like(id = Mapper.ExtIdToIntIdCreate(id), ts = ExtToIntTS(ts) );
        }

        // check if mapping succeeded, return false if not
        public bool ToInternal(out Like like)
        {
            like.ts = ExtToIntTS(ts);
            return Mapper.ExtIdToIntId(id, out like.id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Like operator +(Like l1, Like l2)
        {
            if (l1.GetId() != l2.GetId())
                throw new InvalidOperationException("Like IDs must match");
            var totalLikes = l1.GetNLikes() + l2.GetNLikes();
            var newid = l1.GetId() | ((totalLikes - 1) << 21);
            return new Like { id = newid, ts = l1.ts + l2.ts };
        }

        // comparer class for likes based on ID
        public class LikeComparerById : IComparer<Like>
        {
            public int Compare(Like x, Like y) => x.GetId().CompareTo(y.GetId());
        }
        public static IComparer<Like> CompareById = new LikeComparerById();
    }

}

