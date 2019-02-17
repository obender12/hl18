using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hl18
{
    public partial class Storage
    {
        // primary bitmaps (updated on insert/change)
        public BitMap All = new BitMap(MAX_ACCOUNTS); // all registered users
        public BitMap Male = new BitMap(MAX_ACCOUNTS); // males bitmap
        public BitMap Female = new BitMap(MAX_ACCOUNTS); // males bitmap
        public BitMap Free = new BitMap(MAX_ACCOUNTS); // status: free 
        public BitMap NotFree = new BitMap(MAX_ACCOUNTS); // status: not free 
        public BitMap Taken = new BitMap(MAX_ACCOUNTS); // status: taken
        public BitMap NotTaken = new BitMap(MAX_ACCOUNTS); // status: not taken
        public BitMap Complicated = new BitMap(MAX_ACCOUNTS); // status: complicated
        public BitMap NotComplicated = new BitMap(MAX_ACCOUNTS); // status: not complicated
        public BitMap PremiumNow = new BitMap(MAX_ACCOUNTS); // premium account
        public BitMap PremiumNowNot = new BitMap(MAX_ACCOUNTS); // not a premium account
        public BitMap PremiumYes = new BitMap(MAX_ACCOUNTS); // premium account exits(ed)
        public BitMap PremiumNo = new BitMap(MAX_ACCOUNTS); // is not and was not premium account 
        public BitMap FnameYes = new BitMap(MAX_ACCOUNTS); // users with a first name
        public BitMap FnameNo = new BitMap(MAX_ACCOUNTS); // users without a first name
        public BitMap SnameYes = new BitMap(MAX_ACCOUNTS); // users with a second name
        public BitMap SnameNo = new BitMap(MAX_ACCOUNTS); // users without a second name
        public BitMap PhoneYes = new BitMap(MAX_ACCOUNTS); // user with a phone
        public BitMap PhoneNo = new BitMap(MAX_ACCOUNTS); // user without a phone
        public BitMap CountryYes = new BitMap(MAX_ACCOUNTS); // country selected
        public BitMap CountryNo = new BitMap(MAX_ACCOUNTS); // country not selected
        public BitMap CityYes = new BitMap(MAX_ACCOUNTS); // city specified
        public BitMap CityNo = new BitMap(MAX_ACCOUNTS); // city not specified

        // bitmap based bags
        public RangeBag<int> BirthYears = new BitmapBag<int>(MAX_ACCOUNTS); // named bitmaps by year of birth
        public RangeBag<int> JoinYears = new BitmapBag<int>(MAX_ACCOUNTS); // named bitmaps by year of birth
        public RangeBag<AString> Domains = new BitmapBag<AString>(MAX_ACCOUNTS); // named bitmaps by domain
        public RangeBag<AString> Interests = new BitmapBag<AString>(MAX_ACCOUNTS); // named bitmaps by interests
        public RangeBag<AString> Countries = new BitmapBag<AString>(MAX_ACCOUNTS); // named indices for countries
        public RangeBag<AString> Cities = new BitmapBag<AString>(MAX_ACCOUNTS); // named indices for cities
        public RangeBag<AString> AreaCodes = new BitmapBag<AString>(MAX_ACCOUNTS); // named bitmaps for area codes
        public RangeBag<AString> Fnames = new BitmapBag<AString>(MAX_ACCOUNTS); // named bitmaps by first name
        public RangeBag<int> Snames2 = new BitmapBag<int>(MAX_ACCOUNTS); // named bitmaps by first one or two letters of last name

        // bag of all single ranges
        public RangeBag<AString> Singles = new RangeBag<AString>();

        // combined bitmaps

        // 2x combined (for filter)
        public BitMap CountryYesMale = new BitMap(MAX_ACCOUNTS);
        public BitMap CountryYesFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap CountryNoMale = new BitMap(MAX_ACCOUNTS);
        public BitMap CountryNoFemale = new BitMap(MAX_ACCOUNTS);

        public BitMap CityYesMale = new BitMap(MAX_ACCOUNTS);
        public BitMap CityYesFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap CityNoMale = new BitMap(MAX_ACCOUNTS);
        public BitMap CityNoFemale = new BitMap(MAX_ACCOUNTS);

        public BitMap FreeMale = new BitMap(MAX_ACCOUNTS);
        public BitMap FreeFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotFreeMale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotFreeFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap TakenMale = new BitMap(MAX_ACCOUNTS);
        public BitMap TakenFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotTakenMale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotTakenFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap ComplicatedMale = new BitMap(MAX_ACCOUNTS);
        public BitMap ComplicatedFemale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotComplicatedMale = new BitMap(MAX_ACCOUNTS);
        public BitMap NotComplicatedFemale = new BitMap(MAX_ACCOUNTS);

        // 3x combined (for recommend)
        public BitMap PremiumFreeMale = new BitMap(MAX_ACCOUNTS); // premium and free
        public BitMap PremiumFreeFemale = new BitMap(MAX_ACCOUNTS); // premium and free
        public BitMap PremiumTakenMale = new BitMap(MAX_ACCOUNTS); // premium and taken
        public BitMap PremiumTakenFemale = new BitMap(MAX_ACCOUNTS); // premium and taken
        public BitMap PremiumComplicatedMale = new BitMap(MAX_ACCOUNTS); // premium and complicated
        public BitMap PremiumComplicatedFemale = new BitMap(MAX_ACCOUNTS); // premium and complicated
        public BitMap NonPremiumFreeMale = new BitMap(MAX_ACCOUNTS); // non-premium and free
        public BitMap NonPremiumFreeFemale = new BitMap(MAX_ACCOUNTS); // non-premium and free
        public BitMap NonPremiumTaken = new BitMap(MAX_ACCOUNTS); // non-premium and taken
        public BitMap NonPremiumTakenMale = new BitMap(MAX_ACCOUNTS); // non-premium and taken
        public BitMap NonPremiumTakenFemale = new BitMap(MAX_ACCOUNTS); // non-premium and taken
        public BitMap NonPremiumComplicatedMale = new BitMap(MAX_ACCOUNTS); // non-premium and complicated
        public BitMap NonPremiumComplicatedFemale = new BitMap(MAX_ACCOUNTS); // non-premium and complicated

        private void initSinglesBag()
        {
            Singles.Add("All", All);
            Singles.Add("Male", Male);
            Singles.Add("Female", Female);
            Singles.Add("Free", Free);
            Singles.Add("NotFree", NotFree);
            Singles.Add("Taken", Taken);
            Singles.Add("NotTaken", NotTaken);
            Singles.Add("Complicated", Complicated);
            Singles.Add("NotComplicated", NotComplicated);
            Singles.Add("PremiumNow", PremiumNow);
            Singles.Add("PremiumNowNot", PremiumNowNot);
            Singles.Add("PremiumYes", PremiumYes);
            Singles.Add("PremiumNo", PremiumNo);
            Singles.Add("FnameYes", FnameYes);
            Singles.Add("FnameNo", FnameNo);
            Singles.Add("SnameYes", SnameYes);
            Singles.Add("SnameNo", SnameNo);
            Singles.Add("PhoneYes", PhoneYes);
            Singles.Add("PhoneNo", PhoneNo);
            Singles.Add("CountryYes", CountryYes);
            Singles.Add("CountryNo", CountryNo);
            Singles.Add("CityYes", CityYes);
            Singles.Add("CityNo", CityNo);
        }

        // helper to (re)set forward and reverse bitmaps
        private void updateBitmapPair(int id, BitMap positive, BitMap negative, bool isPositive)
        {
            positive.Set(id, isPositive);
            negative.Set(id, !isPositive);
        }

        private void prepareBitmaps()
        {
            Log.Info("Started updating presence bitmaps");
            for (int id = 1; id <= MAX_ACCOUNTS; id++)
                if (All[id])
                {
                    var acct = Accounts[id];
                    // update the presense bitmaps
                    updateBitmapPair(id, Male, Female, acct.IsMale());
                    updateBitmapPair(id, Free, NotFree, acct.IsFree());
                    updateBitmapPair(id, Taken, NotTaken, acct.IsTaken());
                    updateBitmapPair(id, Complicated, NotComplicated, acct.IsComplicated());
                    updateBitmapPair(id, PremiumNow, PremiumNowNot, acct.IsPremium());
                    updateBitmapPair(id, PremiumYes, PremiumNo, acct.PStart != 0 || acct.PFinish != 0);
                    updateBitmapPair(id, FnameYes, FnameNo, acct.FNameIdx > 0);
                    updateBitmapPair(id, SnameYes, SnameNo, acct.SNameIdx > 0);
                    updateBitmapPair(id, PhoneYes, PhoneNo, acct.Phone != null);
                    updateBitmapPair(id, CountryYes, CountryNo, acct.CountryIdx > 0);
                    updateBitmapPair(id, CityYes, CityNo, acct.CityIdx > 0);
                }
            Log.Info("Finished updating presence bitmaps");


            Log.Info("Prepare bitmaps...");
            Singles.Prepare();
            foreach (var range in Singles)
                Console.Write("{0}: {1}, ", range.Name, range.Count);

            BirthYears.Prepare();
            Console.Write("Birth: {0}, ", BirthYears.Count);

            JoinYears.Prepare();
            Console.Write("Joined: {0}, ", JoinYears.Count);

            Interests.Prepare();
            Console.Write("Interests: {0}, ", Interests.Count);

            Fnames.Prepare();
            Console.Write("Fnames: {0}, ", Fnames.Count);
            Console.Write("Snames: {0}, ", Snames.Count);

            Countries.Prepare();
            Console.Write("Countries: {0}, ", Countries.Count);

            Cities.Prepare();
            Console.Write("Cities: {0}, ", Cities.Count);

            AreaCodes.Prepare();
            Console.Write("AreaCodes: {0}, ", AreaCodes.Count);

            Domains.Prepare();
            Console.Write("Domains: {0}, ", Domains.Count);

            Snames2.Prepare();
            Console.Write("Snames2: {0}, ", Snames2.Count);

            // combined bitmaps 2x
            CountryYesMale.From(CountryYes);
            CountryYesMale.And(Male);
            Console.Write("CountryYesMale: {0}, ", CountryYesMale.Count);
            CountryYesFemale.From(CountryYes);
            CountryYesFemale.And(Female);
            Console.Write("CountryYesFemale: {0}, ", CountryYesFemale.Count);
            CountryNoMale.From(CountryNo);
            CountryNoMale.And(Male);
            Console.Write("CountryNoMale: {0}, ", CountryNoMale.Count);
            CountryNoFemale.From(CountryNo);
            CountryNoFemale.And(Female);
            Console.Write("CountryNoFemale: {0}, ", CountryNoFemale.Count);

            CityYesMale.From(CityYes);
            CityYesMale.And(Male);
            Console.Write("CityYesMale: {0}, ", CityYesMale.Count);
            CityYesFemale.From(CityYes);
            CityYesFemale.And(Female);
            Console.Write("CityYesFemale: {0}, ", CityYesFemale.Count);
            CityNoMale.From(CityNo);
            CityNoMale.And(Male);
            Console.Write("CityNoMale: {0}, ", CityNoMale.Count);
            CityNoFemale.From(CityNo);
            CityNoFemale.And(Female);
            Console.Write("CityNoFemale: {0}, ", CityNoFemale.Count);

            FreeMale.From(Free);
            FreeMale.And(Male);
            Console.Write("FreeMale: {0}, ", FreeMale.Count);
            FreeFemale.From(Free);
            FreeFemale.And(Female);
            Console.Write("FreeFemale: {0}, ", FreeFemale.Count);
            NotFreeMale.From(NotFree);
            NotFreeMale.And(Male);
            Console.Write("NotFreeMale: {0}, ", NotFreeMale.Count);
            NotFreeFemale.From(NotFree);
            NotFreeFemale.And(Female);
            Console.Write("NotFreeFemale: {0}, ", NotFreeFemale.Count);

            TakenMale.From(Taken);
            TakenMale.And(Male);
            Console.Write("TakenMale: {0}, ", TakenMale.Count);
            TakenFemale.From(Taken);
            TakenFemale.And(Female);
            Console.Write("TakenFemale: {0}, ", TakenFemale.Count);
            NotTakenMale.From(NotTaken);
            NotTakenMale.And(Male);
            Console.Write("NotTakenMale: {0}, ", NotTakenMale.Count);
            NotTakenFemale.From(NotTaken);
            NotTakenFemale.And(Female);
            Console.Write("NotTakenFemale: {0}, ", NotTakenFemale.Count);

            ComplicatedMale.From(Complicated);
            ComplicatedMale.And(Male);
            Console.Write("ComplicatedMale: {0}, ", ComplicatedMale.Count);
            ComplicatedFemale.From(Complicated);
            ComplicatedFemale.And(Female);
            Console.Write("ComplicatedFemale: {0}, ", ComplicatedFemale.Count);
            NotComplicatedMale.From(NotComplicated);
            NotComplicatedMale.And(Male);
            Console.Write("NotComplicatedMale: {0}, ", NotComplicatedMale.Count);
            NotComplicatedFemale.From(NotComplicated);
            NotComplicatedFemale.And(Female);
            Console.Write("NotComplicatedFemale: {0}, ", NotComplicatedFemale.Count);

            // combined bitmaps 3x
            PremiumFreeMale.From(PremiumNow);
            PremiumFreeMale.And(Free);
            PremiumFreeMale.And(Male);
            Console.Write("PremiumFreeMale: {0}, ", PremiumFreeMale.Count);

            PremiumFreeFemale.From(PremiumNow);
            PremiumFreeFemale.And(Free);
            PremiumFreeFemale.And(Female);
            Console.Write("PremiumFreeFemale: {0}, ", PremiumFreeFemale.Count);

            PremiumTakenMale.From(PremiumNow);
            PremiumTakenMale.And(Taken);
            PremiumTakenMale.And(Male);
            Console.Write("PremiumTakenMale: {0}, ", PremiumTakenMale.Count);

            PremiumTakenFemale.From(PremiumNow);
            PremiumTakenFemale.And(Taken);
            PremiumTakenFemale.And(Female);
            Console.Write("PremiumTakenFemale: {0}, ", PremiumTakenFemale.Count);

            PremiumComplicatedMale.From(PremiumNow);
            PremiumComplicatedMale.And(Complicated);
            PremiumComplicatedMale.And(Male);
            Console.Write("PremiumComplicatedMale: {0}, ", PremiumComplicatedMale.Count);

            PremiumComplicatedFemale.From(PremiumNow);
            PremiumComplicatedFemale.And(Complicated);
            PremiumComplicatedFemale.And(Female);
            Console.Write("PremiumComplicatedFemale: {0}, ", PremiumComplicatedFemale.Count);

            NonPremiumFreeMale.From(PremiumNowNot);
            NonPremiumFreeMale.And(Free);
            NonPremiumFreeMale.And(Male);
            Console.Write("NonPremiumFreeMale: {0}, ", NonPremiumFreeMale.Count);

            NonPremiumFreeFemale.From(PremiumNowNot);
            NonPremiumFreeFemale.And(Free);
            NonPremiumFreeFemale.And(Female);
            Console.Write("NonPremiumFreeFemale: {0}, ", NonPremiumFreeFemale.Count);

            NonPremiumTakenMale.From(PremiumNowNot);
            NonPremiumTakenMale.And(Taken);
            NonPremiumTakenMale.And(Male);
            Console.Write("NonPremiumTakenMale: {0}, ", NonPremiumTakenMale.Count);

            NonPremiumTakenFemale.From(PremiumNowNot);
            NonPremiumTakenFemale.And(Taken);
            NonPremiumTakenFemale.And(Female);
            Console.Write("NonPremiumTakenFemale: {0}, ", NonPremiumTakenFemale.Count);

            NonPremiumComplicatedMale.From(PremiumNowNot);
            NonPremiumComplicatedMale.And(Complicated);
            NonPremiumComplicatedMale.And(Male);
            Console.Write("NonPremiumComplicatedMale: {0}, ", NonPremiumComplicatedMale.Count);

            NonPremiumComplicatedFemale.From(PremiumNowNot);
            NonPremiumComplicatedFemale.And(Complicated);
            NonPremiumComplicatedFemale.And(Female);
            Console.Write("NonPremiumComplicatedFemale: {0}, ", NonPremiumComplicatedFemale.Count);

            Console.WriteLine("done");

        }
    }
}
