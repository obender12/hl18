using System;
using System.Collections.Generic;
using System.Text;

namespace hl18
{
    // !! Storage only works with internal IDs and transformed likes
    public partial class Storage
    {
        // assumptions
        public static int MAX_ACCOUNTS = 1_360_000;

        // huuuuuge arrays
        public Account[] Accounts = new Account[MAX_ACCOUNTS];
        public Like[] Likes = new Like[75_000_000]; // 600 Mb
        public int[] LikedBy = new int[70_000_000]; // 280 Mb
        public int NextLike = 16;
        public int NextLikedBy = 16;

        // uniqueness or string deduplication bags
        public RangeBag<AString> Snames = new RangeBagOf<AString, CounterRange>(2000);
        public HashSet<byte[]> Emails = new HashSet<byte[]>(MAX_ACCOUNTS, ByteArrayComparer.Instance);

        // similarity cache
        public Dictionary<long, double> CachedSim = new Dictionary<long, double>(1_000_000);

        // constructor
        public Storage()
        {
            // compose the bag of singles
            initSinglesBag();
        }

        // API

        public static DateTime MinJoined = new DateTime(2011, 1, 1);
        public static DateTime MaxJoined = new DateTime(2018, 1, 1);
        public static DateTime MinPremium = new DateTime(2018, 1, 1);
        public static string s_Male = "m";
        public static string s_Female = "f";
        public static readonly string s_Free = "свободны";
        public static readonly string s_Taken = "заняты";
        public static readonly string s_Complicated = "всё сложно";


        // called after initial loading, or after finishing stage 2
        public void Prepare(bool init)
        {
            // pre-calculate bitmap stats
            prepareBitmaps();

            // process dorty likes and likedBy lists
            processDirtyLikes();

            Log.Info("Done finalizing the update");
        }

    }


}
