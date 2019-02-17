using System;

namespace hl18
{
    public class Warmup
    {

        private readonly Router router;
        private readonly Storage storage;
        private readonly Random rnd;

        public Warmup(Router router, Storage storage)
        {
            this.router = router;
            this.storage = storage;
            rnd = new Random();
        }

        private static double PROB_FILTER = 0.503;
        private static double PROB_GROUP = PROB_FILTER + 0.195;
        private static double PROB_RECOMMEND = PROB_GROUP + 0.179;
        private static double PROB_SUGGEST = PROB_RECOMMEND + 0.179;

        public void RunGet(TimeSpan warmupDeadline)
        {
            HttpCtx ctx = new HttpCtx();
            int query_id = 0;
            while (Stats.Watch.Elapsed < warmupDeadline)
                GetOnce(ctx, query_id++);
        }

        public void GetOnce(HttpCtx ctx, int query_id)
        {
            if (storage.Cities.Count > 1)
            {
                ctx.Reset();
                var r = rnd.NextDouble();
                if (r < PROB_FILTER)
                    getFilter(query_id, ctx);
                else
                if (r < PROB_GROUP)
                    getGroup(query_id, ctx);
                else
                if (r < PROB_RECOMMEND)
                    getRecommend(query_id, ctx);
                else
                    getSuggest(query_id, ctx);
            }
        }



        private void getFilter(int queryId, HttpCtx ctx)
        {
            var query = ctx.Params;
            query.Add("query_id", queryId.ToString());
            query.Add("limit", getRandom(1, 50));
            double total = 51197.0;
            int maxOr = 0;

            // sex
            if (rnd.NextDouble() < 13309 / total)
                query.Add("sex_eq", getRandom(new[] {Storage.s_Male, Storage.s_Female}));

            // status
            if (rnd.NextDouble() < 6613 / total)
                query.Add("status_eq", getRandom(new[] { Storage.s_Free, Storage.s_Taken, Storage.s_Complicated }));

            // country
            if (rnd.NextDouble() < 3689 / total)
                query.Add("country_eq", storage.Countries[ getRandom(1, storage.Countries.Count) ].Name);
            else
            if (rnd.NextDouble() < 3560 / total)
                query.Add("country_null", getRandom(0, 2));

            // interests
            if (rnd.NextDouble() < 3000 / total)
                for (int i = 1; i <= 3; i++)
                    query.Add("interests_contains", storage.Interests[getRandom(1, storage.Interests.Count)].Name);
            else
            if (rnd.NextDouble() < 2969 / total && maxOr < 2)
            {
                for (int i = 1; i <= 6; i++)
                    query.Add("interests_any", storage.Interests[getRandom(1, storage.Interests.Count)].Name);
                maxOr++;
            }

            // birth
            if (rnd.NextDouble() < 2890 / total)
                query.Add(getRandom(new[] { "birth_lt", "birth_gt" }), 
                    getRandom( new DateTime(1950,1,1).ToUnix(), Storage.MinPremium.ToUnix() ) );
            else
            if (rnd.NextDouble() < 1420 / total)
                query.Add("birth_year", storage.BirthYears[getRandom(1, storage.BirthYears.Count)].Name);

            // likes_contains
            if (rnd.NextDouble() < 2137 / total)
                for (int i = 1; i <= 4; i++)
                    query.Add("likes_contains", getRandom(1, Storage.MAX_ACCOUNTS) );

            // email
            if (rnd.NextDouble() < 1934 / total)
                query.Add(getRandom(new[] { "email_lt", "email_gt" }),
                    (char) getRandom(97,97+26) + 
                    (char) getRandom(97,97+26) );
            else
            if (rnd.NextDouble() < 924 / total)
                query.Add("email_domain", storage.Domains[getRandom(1, storage.Domains.Count)].Name);

            // city
            if (rnd.NextDouble() < 1541 / total)
                query.Add("city_eq", storage.Cities[getRandom(1, storage.Cities.Count)].Name);
            else
            if (rnd.NextDouble() < 1475 / total && maxOr < 2)
            {
                for (int i = 1; i <= 6; i++)
                    query.Add("city_any", storage.Cities[getRandom(1, storage.Cities.Count)].Name);
                maxOr++;
            }
            else
            if (rnd.NextDouble() < 1468 / total)
                query.Add("city_null", getRandom(0, 2));

            // premium 
            if (rnd.NextDouble() < 1113 / total)
                query.Add("premium_now", getRandom(0,2));

            // sname
            if (rnd.NextDouble() < 511 / total)
                query.Add("sname_starts", "ab");
            else
            if (rnd.NextDouble() < 505 / total)
                query.Add("sname_null", getRandom(0, 2));

            // fname
            if (rnd.NextDouble() < 498 / total)
                query.Add("fname_null", getRandom(0, 2));
            else
            if (rnd.NextDouble() < 482 / total && maxOr < 2)
            {
                for (int i = 1; i <= 6; i++)
                    query.Add("fname_any", storage.Fnames[getRandom(1, storage.Fnames.Count)].Name);
                maxOr++;
            }

            // phone
            if (rnd.NextDouble() < 397 / total)
                query.Add("phone_null", getRandom(0, 2));
            else
            if (rnd.NextDouble() < 365 / total)
                query.Add("phone_code", storage.AreaCodes[getRandom(1, storage.AreaCodes.Count)].Name);

            var queryStr = query.ToString();
            var statusCode = router.getFilter.Process(ctx, 0);
            if ( statusCode != 200)
                router.getFilter.Process(ctx, 0); // debug
        }

        private void getGroup(int queryId, HttpCtx ctx)
        {
            var query = ctx.Params;
            query.Add("query_id", queryId.ToString());
            query.Add("limit", getRandom(1, 50));
            double total = 19729.0;
            bool countryUsed = false;
            bool cityUsed = false;
            bool birthUsed = false;

            // key status
            if (rnd.NextDouble() < 2618 / total)
                query.Add("keys", "status");

            // key status
            if (rnd.NextDouble() < 1895 / total)
                query.Add("keys", "sex");

            // key country
            if (rnd.NextDouble() < 1922 / total)
            {
                query.Add("keys", "country");
                countryUsed = true;
            }
            else
            // key city
            if (rnd.NextDouble() < 1871 / total && !countryUsed)
            {
                query.Add("keys", "city");
                cityUsed = true;
            }

            // key interests
            if (rnd.NextDouble() < 1116 / total && !countryUsed)
                query.Add("keys", "interests");

            // birth
            if (rnd.NextDouble() < 2496 / total)
            {
                query.Add("birth", storage.BirthYears[getRandom(1, storage.BirthYears.Count)].Name);
                birthUsed = true;
            }
            else
            // joined
            if (rnd.NextDouble() < 2496 / total && !birthUsed)
            {
                query.Add("joined", storage.JoinYears[getRandom(1, storage.JoinYears.Count)].Name);
                birthUsed = true;
            }

            // sex
            if (rnd.NextDouble() < 911 / total)
                query.Add("sex", getRandom(new[] { Storage.s_Male, Storage.s_Female }));

            // country
            if (rnd.NextDouble() < 776 / total && !cityUsed)
            {
                query.Add("country", storage.Countries[getRandom(1, storage.Countries.Count)].Name);
                countryUsed = true;
            }

            // country
            if (rnd.NextDouble() < 737 / total && !cityUsed)
            {
                query.Add("country", storage.Countries[getRandom(1, storage.Countries.Count)].Name);
                cityUsed = true;
            }

            // interests
            if (rnd.NextDouble() < 948 / total)
                query.Add("interests", storage.Interests[getRandom(1, storage.Interests.Count)].Name);

            // likes_contains
            if (rnd.NextDouble() < 1482 / total)
                query.Add("likes", getRandom(1, Storage.MAX_ACCOUNTS));

            var queryStr = query.ToString();
            var statusCode = router.getGroup.Process(ctx, 0);
            if (statusCode != 200)
                router.getGroup.Process(ctx, 0); // debug
        }

        private void getRecommend(int queryId, HttpCtx ctx)
        {
            var query = ctx.Params;
            query.Add("query_id", queryId.ToString());
            query.Add("limit", getRandom(1, 50));
            double total = 6958.0;

            // country
            if (rnd.NextDouble() < 2663 / total)
                query.Add("country", storage.Countries[getRandom(1, storage.Countries.Count)].Name);

            // city
            if (rnd.NextDouble() < 2608 / total)
                query.Add("city", storage.Cities[getRandom(1, storage.Cities.Count)].Name);

            var queryStr = query.ToString();
            var id = getRandom(1, Storage.MAX_ACCOUNTS);
            var statusCode = router.getRecommend.Process(ctx, id);
            if (statusCode != 200)
                router.getRecommend.Process(ctx, id); // debug
        }


        private void getSuggest(int queryId, HttpCtx ctx)
        {
            var query = ctx.Params;
            query.Add("query_id", queryId.ToString());
            query.Add("limit", getRandom(1, 50));
            double total = 4475.0;

            // country
            if (rnd.NextDouble() < 1683 / total)
                query.Add("country", storage.Countries[getRandom(1, storage.Countries.Count)].Name);

            // city
            if (rnd.NextDouble() < 1659 / total)
                query.Add("city", storage.Cities[getRandom(1, storage.Cities.Count)].Name);

            var queryStr = query.ToString();
            var id = getRandom(1, Storage.MAX_ACCOUNTS);
            var statusCode = router.getSuggest.Process(ctx, id);
            if (statusCode != 200)
                router.getSuggest.Process(ctx, id); // debug
        }


        // helpers
        private int getRandom(int from, int to)
        {
            return rnd.Next(from, to);
        }

        private string getRandom(string[] strs)
        {
            return strs[rnd.Next(strs.Length)];
        }

    }
}
