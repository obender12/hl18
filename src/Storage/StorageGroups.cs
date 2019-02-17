#define USE_HYPERCUBE_ARRAY
//#define USE_HYPERCUBE_HASH
//#define USE_HYPERCUBE_TREE

namespace hl18
{
    public partial class Storage
    {
        // Indices: [Location (country OR city)][status][sex][Year (birth OR joined)][Interests]
        // Index: -1=aggregate, 1..MAX for specified value, 0 for non-specified value (e.g. city,country)

#if USE_HYPERCUBE_ARRAY
        public HypercubeArray CubeCityBirth = new HypercubeArray(CubeKind.CityBirth, 615, 4, 3, 30, 96);
        public HypercubeArray CubeCityJoined = new HypercubeArray(CubeKind.CityJoined, 615, 4, 3, 9, 96);
        public HypercubeArray CubeCountryBirth = new HypercubeArray(CubeKind.CountryBirth, 75, 4, 3, 30, 96);
        public HypercubeArray CubeCountryJoined = new HypercubeArray(CubeKind.CountryJoined, 75, 4, 3, 9, 96);
#elif USE_HYPERCUBE_HASH
        public HypercubeHash CubeCityBirth = new HypercubeHash(CubeKind.CityBirth, 4_000_000);
        public HypercubeHash CubeCityJoined = new HypercubeHash(CubeKind.CityJoined, 4_000_000);
        public HypercubeHash CubeCountryBirth = new HypercubeHash(CubeKind.CountryBirth, 4_000_000);
        public HypercubeHash CubeCountryJoined = new HypercubeHash(CubeKind.CountryJoined, 4_000_000);
#elif USE_HYPERCUBE_TREE
        public HypercubeTree CubeCityBirth = new HypercubeTree(CubeKind.CityBirth);
        public HypercubeTree CubeCityJoined = new HypercubeTree(CubeKind.CityJoined);
        public HypercubeTree CubeCountryBirth = new HypercubeTree(CubeKind.CountryBirth);
        public HypercubeTree CubeCountryJoined = new HypercubeTree(CubeKind.CountryJoined);
#endif
        private void updateGroups(ref Account acct, bool include)
        {
            int statusIdx = (acct.Flags >> 1) & 3;
            int sexIdx = (acct.Flags & Account.Male) > 0 ? 1 : 2;
            int cityIdx = acct.CityIdx;
            int countryIdx = acct.CountryIdx;

            if( include )
            {
                CubeCityBirth.Include(cityIdx, statusIdx, sexIdx, acct.BirthIdx, acct.InterestMask);
                CubeCityJoined.Include(cityIdx, statusIdx, sexIdx, acct.JoinedIdx, acct.InterestMask);
                CubeCountryBirth.Include(countryIdx, statusIdx, sexIdx, acct.BirthIdx, acct.InterestMask);
                CubeCountryJoined.Include(countryIdx, statusIdx, sexIdx, acct.JoinedIdx, acct.InterestMask);
            }
            else
            {
                CubeCityBirth.Exclude(cityIdx, statusIdx, sexIdx, acct.BirthIdx, acct.InterestMask);
                CubeCityJoined.Exclude(cityIdx, statusIdx, sexIdx, acct.JoinedIdx, acct.InterestMask);
                CubeCountryBirth.Exclude(countryIdx, statusIdx, sexIdx, acct.BirthIdx, acct.InterestMask);
                CubeCountryJoined.Exclude(countryIdx, statusIdx, sexIdx, acct.JoinedIdx, acct.InterestMask);
            }
        }
    }
}
