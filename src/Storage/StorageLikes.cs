using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace hl18
{
    public partial class Storage
    {
        // pre-validation
        public int VerifyNewLikes(DtoLikes dtoLikes)
        {
            bool lockTaken = false;
            try
            {
                updateLock.Enter(ref lockTaken);
                if (!verifyLikes(dtoLikes.likes))
                    return 400;
            }
            finally
            {
                if (lockTaken)
                    updateLock.Exit();
            }

            return 202;
        }

        // post-processing, serialized
        public void PostNewLikes(DtoLikes dtoLikes)
        {
            for (int i = 0; i < dtoLikes.likes.Count; i++)
            {
                var dtoLike = dtoLikes.likes[i];
                var id = dtoLike.liker;
                var acct = Accounts[id];
                var like = new Like { id = dtoLike.likee, ts = dtoLike.ts };
                addLike(id, ref acct, like);
                Accounts[id] = acct;
            }
        }


        // helper to verify dtoLikes (3-way likes)
        private bool verifyLikes(List<DtoLike> dtoLikes)
        {
            foreach (var dtoLike in dtoLikes)
            {
                if (dtoLike.liker < 0 || dtoLike.liker >= MAX_ACCOUNTS)
                    return false; // out of range
                if (!All[dtoLike.liker])
                    return false; // liker does not exist
                if (dtoLike.likee < 0 || dtoLike.likee >= MAX_ACCOUNTS)
                    return false; // out of range
                if (!All[dtoLike.likee])
                    return false; // likee does not exist
            }
            return true;
        }

        // helper to verify 2-way likes
        private bool verifyLikes(List<Like> likes)
        {
            foreach (var like in likes)
            {
                if (like.id < 0 || like.id >= MAX_ACCOUNTS)
                    return false; // out of range
                if (!All[like.id])
                    return false; // likee does not exist
            }
            return true;
        }

        private void addLikes(int id, ref Account acct, List<Like> likes)
        {
            ensureLikesCapacity(ref acct, acct.LikesCount + likes.Count);

            // we have enough capacity, add likes from the list
            for (int i = 0; i < likes.Count; i++)
                addLike(id, ref acct, likes[i]);
        }

        private void addLike(int id, ref Account acct, Like like)
        {
            // ensure sufficient capacity
            if (acct.LikesCount == acct.LikesCapacity)
                ensureLikesCapacity(ref acct, acct.LikesCapacity+1);

            // store the like
            Likes[acct.LikesIdx + acct.LikesCount++] = like;
            acct.Flags |= Account.LikesDirty;

            // update likedBy
            var likee = Accounts[like.id];
            if (likee.LikedByCount == likee.LikedByCapacity)
                ensureLikedByCapacity(ref likee, likee.LikedByCapacity*2);
            LikedBy[likee.LikedByIdx + likee.LikedByCount++] = id;
            likee.Flags |= Account.LikedByDirty;
            Accounts[like.id] = likee;
        }


        private static int MaxHoles16 = 1000;
        private int[]  holes16 = new int[MaxHoles16];
        private int hole16Count = 0;
        private static int MaxHoles32 = 1000;
        private int[] holes32 = new int[MaxHoles32];
        private int hole32Count = 0;
        private static int MaxHoles48 = 1000;
        private int[] holes48 = new int[MaxHoles48];
        private int hole48Count = 0;
        private static int MaxHoles64 = 1000;
        private int[] holes64 = new int[MaxHoles64];
        private int hole64Count = 0;

        private void ensureLikesCapacity(ref Account acct, int newCapacity)
        {
            // min and max
            if (newCapacity < 16)
                newCapacity = 16;
            else
            {
                // round to the next power of 2
                var v = newCapacity-1;
                v |= v >> 1;
                v |= v >> 2;
                v |= v >> 4;
                newCapacity = v+1;
            }

            if (acct.LikesCapacity < newCapacity)
            {
                // find a new slot
                int moveTo;
                if (newCapacity == 16 && hole16Count > 0)
                    moveTo = holes16[--hole16Count]; else
                if (newCapacity == 32 && hole32Count > 0)
                    moveTo = holes32[--hole32Count]; else
                if (newCapacity == 64 && hole64Count > 0)
                    moveTo = holes64[--hole64Count]; else
                {
                    moveTo = NextLike;
                    NextLike += newCapacity;
                }

                // recycle the old slot
                if (acct.LikesCapacity == 16 && hole16Count < MaxHoles16)
                    holes16[hole16Count++] = acct.LikesIdx; else
                if (acct.LikesCapacity == 32 && hole32Count < MaxHoles32)
                    holes32[hole32Count++] = acct.LikesIdx; else
                if (acct.LikesCapacity == 64 && hole64Count < MaxHoles64)
                    holes64[hole64Count++] = acct.LikesIdx;

                // move existing likes (if any) to the new place
                for (int i = 0; i < acct.LikesCount; i++)
                    Likes[moveTo + i] = Likes[acct.LikesIdx + i];

                // update the pointers
                acct.LikesIdx = moveTo;
                acct.LikesCapacity = (byte)newCapacity;
            }
        }

        private void ensureLikedByCapacity(ref Account acct, int newCapacity)
        {
            // min and max
            if (newCapacity < 48)
                newCapacity = 48;

            if (acct.LikedByCapacity < newCapacity)
            {
                // find a new slot
                int moveTo;
                if (newCapacity == 48 && hole48Count > 0)
                    moveTo = holes48[--hole48Count];
                else
                {
                    moveTo = NextLikedBy;
                    NextLikedBy += newCapacity;
                }

                // move existing likes (if any) to the new place
                for (int i = 0; i < acct.LikedByCount; i++)
                    LikedBy[moveTo + i] = LikedBy[acct.LikedByIdx + i];

                // update the pointers
                acct.LikedByIdx = moveTo;
                acct.LikedByCapacity = (byte)newCapacity;
            }
        }

        private void prepareLikes(int id, ref Account acct)
        {
            // in-place sort the likes in ascending order by id
            Array.Sort(Likes, acct.LikesIdx, acct.LikesCount, Like.CompareById);

            // de-duplicate likes with the same id
            int i = acct.LikesIdx;
            while (i < acct.LikesIdx + acct.LikesCount - 1)
            {
                if (Likes[i].GetId() == Likes[i + 1].GetId())
                {
                    Likes[i] += Likes[i + 1];
                    for (int j = i + 1; j < acct.LikesIdx + acct.LikesCount - 1; j++)
                        Likes[j] = Likes[j + 1];
                    acct.LikesCount--;
                }
                else
                {
                    i++;
                }
            }
        }

        private void prepareLikedBy(int id, ref Account acct)
        {
            // in-place sort the likes in ascending order by id
            Array.Sort(LikedBy, acct.LikedByIdx, acct.LikedByCount);

            // de-duplicate likedBy with the same id
            int i = acct.LikedByIdx;
            while (i < acct.LikedByIdx + acct.LikedByCount - 1)
            {
                if (LikedBy[i] == LikedBy[i + 1])
                {
                    for (int j = i + 1; j < acct.LikedByIdx + acct.LikedByCount - 1; j++)
                        LikedBy[j] = LikedBy[j + 1];
                    acct.LikedByCount--;
                }
                else
                {
                    i++;
                }
            }
        }


        private void processDirtyLikes()
        {
            Log.Info("Prepare likes");
            var nTotalLikes = 0;
            var nAcctsWithLikes = 0;
            var maxLikes = 0;
            var maxLikedBy = 0;

            // go through the list of like-dirty accounts
            foreach (var id in All.Enumerate())
            {
                var acct = Accounts[id];

                // calculate stats
                if (acct.LikesCount > 0)
                {
                    nAcctsWithLikes++;

                    nTotalLikes += acct.LikesCount;
                    if (maxLikes < acct.LikesCount)
                        maxLikes = acct.LikesCount;
                    if (maxLikedBy < acct.LikedByCount)
                        maxLikedBy = acct.LikedByCount;
                }

                if ( (acct.Flags & Account.LikesDirty) != 0 )
                {
                    // sort and deduplicate the likes
                    prepareLikes(id, ref acct);

                    // reset the flag and store the account
                    acct.Flags &= (byte)~Account.LikesDirty;
                    Accounts[id] = acct;
                }

                if ((acct.Flags & Account.LikedByDirty) != 0)
                {
                    // sort and de-duplicate the likedBy list
                    prepareLikedBy(id, ref acct);

                    // reset the flag and store the account
                    acct.Flags &= (byte)~Account.LikedByDirty;
                    Accounts[id] = acct;
                }

            }

            Log.Info("Total likes: " + nTotalLikes);
            Log.Info("Accounts with likes: " + nAcctsWithLikes);
            Log.Info("Average likes: " + nTotalLikes / nAcctsWithLikes);
            Log.Info("Max likes: " + maxLikes);
            Log.Info("Max likedBy: " + maxLikedBy);
            Log.Info("NextLike: " + NextLike);
            Log.Info("NextLikedBy: " + NextLikedBy);
        }

    }
}
