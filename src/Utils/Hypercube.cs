using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace hl18
{

    public enum CubeKind
    {
        None = 0,
        CityBirth = 1,
        CityJoined = 2,
        CountryBirth = 3,
        CountryJoined = 4
    }

    public delegate void CubeVisitor(int location, int status, int sex, int year, int interest, int count);

    public interface IHypercube
    {
        // count in
        void Include(int location, int status, int sex, int year, BitMap96 interestMask);

        // count out
        void Exclude(int location, int status, int sex, int year, BitMap96 interestMask);

        // slice the cube with non-zero counts
        void Slice(
            int locationFrom, int locationTo,
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor);

        CubeKind Kind { get; }
    }

    // nodes are not thread safe
    class RootNode
    {
        private List<LocationNode> locations;
        private LocationNode aggregate;

        public void Update(int location, int status, int sex, int year, BitMap96 interestMask, int change/*+1 or -1*/)
        {
            if (locations == null)
            {
                locations = new List<LocationNode>(LocationNode.MAX_LOCATIONS);
                aggregate = new LocationNode();
            }
            while (location >= locations.Count)
                locations.Add(null);
            if (locations[location] == null)
                locations[location] = new LocationNode();
            locations[location].Update(status, sex, year, interestMask, change);
            aggregate.Update(status, sex, year, interestMask, change);
        }

        public int GetCount(int location, int status, int sex, int year, int interest )
        {
            if (location < 0)
                return aggregate.GetCount(status, sex, year, interest);
            if (locations == null || location >= locations.Count || locations[location] == null)
                return 0;
            return locations[location].GetCount(status, sex, year, interest);
        }

        public void Slice(
            int locationFrom, int locationTo, 
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            if(locations != null)
            {
                if (locationFrom < 0)
                {
                    aggregate.Slice(-1,
                        statusFrom, statusTo,
                        sexFrom, sexTo,
                        yearFrom, yearTo,
                        interestFrom, interestTo,
                        visitor);
                }
                else
                if (locationFrom < locations.Count)
                {
                    for (int location = locationFrom; location <= Math.Min(locationTo, locations.Count - 1); location++)
                        if (locations[location] != null)
                            locations[location].Slice( location,
                                statusFrom, statusTo,
                                sexFrom, sexTo,
                                yearFrom, yearTo,
                                interestFrom, interestTo,
                                visitor);
                }
            }
        }

    }

    class LocationNode
    {
        public static int MAX_LOCATIONS = 680;
        private List<StatusNode> statuses;
        private StatusNode aggregate;

        public void Update(int status, int sex, int year, BitMap96 interestMask, int change/*+1 or -1*/)
        {
            if (statuses == null)
            {
                statuses = new List<StatusNode>(StatusNode.MAX_STATUSES);
                aggregate = new StatusNode();
            }
            while (status >= statuses.Count)
                statuses.Add(null);
            if (statuses[status] == null)
                statuses[status] = new StatusNode();
            statuses[status].Update(sex, year, interestMask, change);
            aggregate.Update(sex, year, interestMask, change);
        }

        public int GetCount(int status, int sex, int year, int interest)
        {
            if (status < 0)
                return aggregate.GetCount(sex, year, interest);
            if ( statuses==null || status >= statuses.Count || statuses[status] == null)
                return 0;
            return statuses[status].GetCount(sex, year, interest);
        }

        public void Slice(int location,
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            if( statuses != null )
            {
                if (statusFrom < 0)
                {
                    aggregate.Slice(location, -1,
                        sexFrom, sexTo,
                        yearFrom, yearTo,
                        interestFrom, interestTo,
                        visitor);
                }
                else
                if (statusFrom < statuses.Count)
                {
                    for (int status = statusFrom; status <= Math.Min(statusTo, statuses.Count - 1); status++)
                        if (statuses[status] != null)
                            statuses[status].Slice(location, status,
                                sexFrom, sexTo,
                                yearFrom, yearTo,
                                interestFrom, interestTo,
                                visitor);
                }
            }
        }
    }

    class StatusNode
    {
        public static int MAX_STATUSES = 4;
        private List<SexNode> sexes;
        private SexNode aggregate;

        public void Update(int sex, int year, BitMap96 interestMask, int change/*+1 or -1*/)
        {
            if (sexes == null)
            {
                sexes = new List<SexNode>(SexNode.MAX_SEXES);
                aggregate = new SexNode();
            }
            while (sex >= sexes.Count)
                sexes.Add(null);
            if (sexes[sex] == null)
                sexes[sex] = new SexNode();
            sexes[sex].Update(year, interestMask, change);
            aggregate.Update(year, interestMask, change);
        }

        public int GetCount(int sex, int year, int interest)
        {
            if (sex < 0)
                return aggregate.GetCount(year, interest); 
            if (sexes==null || sex>=sexes.Count || sexes[sex] == null)
                return 0;
            return sexes[sex].GetCount(year, interest);
        }

        public void Slice(int location, int status, 
            int sexFrom, int sexTo,
            int yearFrom, int yearTo, 
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            if(sexes != null)
            {
                if (sexFrom < 0)
                {
                    aggregate.Slice(location, status, -1,
                        yearFrom, yearTo,
                        interestFrom, interestTo,
                        visitor);
                }
                else
                if (sexFrom < sexes.Count)
                {
                    for (int sex = sexFrom; sex <= Math.Min(sexTo, sexes.Count - 1); sex++)
                        if (sexes[sex] != null)
                            sexes[sex].Slice(location, status, sex,
                                yearFrom, yearTo,
                                interestFrom, interestTo,
                                visitor);
                }
            }
        }
    }

    class SexNode
    {
        public static int MAX_SEXES = 3;
        private List<YearNode> years;
        private YearNode aggregate;

        public void Update(int year, BitMap96 interestMask, int change/*+1 or -1*/)
        {
            if (years == null)
            {
                years = new List<YearNode>(YearNode.MAX_YEARS);
                aggregate = new YearNode();
            }
            while (year >= years.Count)
                years.Add(null);
            if (years[year] == null)
                years[year] = new YearNode();
            years[year].Update(interestMask, change);
            aggregate.Update(interestMask, change);
        }

        public int GetCount(int year, int interest)
        {
            if (year < 0)
                return aggregate.GetCount(interest); ;
            if (years==null || year >= years.Count || years[year] == null )
                return 0;
            return years[year].GetCount(interest);
        }

        public void Slice( int location, int status, int sex,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            if( years != null)
            {
                if (yearFrom < 0)
                {
                    aggregate.Slice(location, status, sex, -1,
                        interestFrom, interestTo,
                        visitor);
                }
                else
                if (yearFrom < years.Count)
                {
                    for (int year = yearFrom; year <= Math.Min(yearTo, years.Count - 1); year++)
                        if (years[year] != null)
                            years[year].Slice(location, status, sex, year,
                                interestFrom, interestTo,
                                visitor);
                }
            }
        }
    }

    class YearNode
    {
        public static int MAX_YEARS = 32;
        private int[] interests; // [0] = aggregate

        public void Update(BitMap96 interestMask, int change/*+1 or -1*/)
        {
            if (interests == null)
                interests = new int[BitMap96.MAX_BITS];
            interests[0] += change; // aggregate
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (interestMask.IsSet(i))
                    interests[i] += change;
        }

        public int GetCount(int interest)
        {
            if (interest < 0)
                return interests[0];
            if (interests == null )
                return 0;
            return interests[interest];
        }

        public void Slice( int location, int status, int sex, int year, 
            int interestFrom, int interestTo, CubeVisitor visitor)
        {
            if (interests != null)
            {
                if (interestFrom < 0)
                {
                    if (interests[0] > 0)
                        visitor(location, status, sex, year, -1, interests[0] /*aggregate*/);
                }
                else
                if (interestFrom < interests.Length)
                {
                    for (int interest = interestFrom; interest <= Math.Min(interestTo, interests.Length - 1); interest++)
                        if( interests[interest]>0)
                            visitor(location, status, sex, year, interest, interests[interest]);
                }
            }
        }
    }



    public class HypercubeTree: IHypercube
    {
        readonly CubeKind cubeKind;
        RootNode root = new RootNode();

        // constructor
        public HypercubeTree( CubeKind cubeKind )
        {
            this.cubeKind = cubeKind;
        }

        // count in
        public void Include(int location, int status, int sex, int year, BitMap96 interestMask)
        {
            Debug.Assert(location >= 0);
            Debug.Assert(status > 0);
            Debug.Assert(sex > 0);
            Debug.Assert(year > 0);
            root.Update(location, status, sex, year, interestMask, +1);
        }

        // count out
        public void Exclude(int location, int status, int sex, int year, BitMap96 interestMask)
        {
            Debug.Assert(location >= 0);
            Debug.Assert(status >= 0);
            Debug.Assert(sex >= 0);
            Debug.Assert(year >= 0);
            root.Update(location, status, sex, year, interestMask, -1);
        }

        // getter
        public int this[int i1, int i2, int i3, int i4, int i5]
        {
            get { return root.GetCount(i1,i2,i3,i4,i5); }
        }

        // slice the cube with non-zero counts
        public void Slice(
            int locationFrom, int locationTo,
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            root.Slice(
                locationFrom, locationTo, 
                statusFrom, statusTo, 
                sexFrom, sexTo, 
                yearFrom, yearTo, 
                interestFrom, interestTo, 
                visitor);
        }

        public CubeKind Kind { get => cubeKind; }
    }


    public class HypercubeArray: IHypercube
    {
        readonly int[,,,,] counters;
        // max number per category
        readonly int maxLocations;
        readonly int maxStatuses;
        readonly int maxSexes;
        readonly int maxYears;
        readonly int maxInterests;
        // index for aggregates
        readonly int aggLocation;
        readonly int aggStatus;
        readonly int aggSex;
        readonly int aggYear;
        readonly int aggInterest;
        CubeKind cubeKind;

        // constructor
        public HypercubeArray(
            CubeKind cubeKind,
            int maxLocations,
            int maxStatuses,
            int maxSexes,
            int maxYears,
            int maxInterests)
        {
            this.cubeKind = cubeKind;
            this.maxLocations = maxLocations;       aggLocation = maxLocations;
            this.maxStatuses = maxStatuses;         aggStatus = 0;
            this.maxSexes = maxSexes;               aggSex = 0;
            this.maxYears = maxYears;               aggYear = 0;
            this.maxInterests = maxInterests;       aggInterest = 0;
            
            // allocate one extra element for aggregates
            counters = new int[maxLocations+1 /*one extra for the aggregate*/, maxStatuses, maxSexes, maxYears, maxInterests];
        }


        // count in
        public void Include(int location, int status, int sex, int year, BitMap96 interests)
        {
            updateAll(location, status, sex, year, aggInterest, +1);
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (interests.IsSet(i))
                    updateAll(location, status, sex, year, i, +1);
        }

        // count out
        public void Exclude(int location, int status, int sex, int year, BitMap96 interests)
        {
            updateAll(location, status, sex, year, aggInterest, -1);
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (interests.IsSet(i))
                    updateAll(location, status, sex, year, i, -1);
        }

        // getter (watch for -1!)
        public int this[int i1, int i2, int i3, int i4, int i5]
        {
            get { return (counters[i1, i2, i3, i4, i5]); }
        }

        // slice the cube with non-zero counts
        public void Slice(
            int locationFrom, int locationTo,
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            // check for aggregates
            if (locationFrom < 0) locationFrom = locationTo = aggLocation;
            if (statusFrom < 0) statusFrom = statusTo = aggStatus;
            if (sexFrom < 0) sexFrom = sexTo = aggSex;
            if (yearFrom < 0) yearFrom = yearTo = aggYear;
            if (interestFrom < 0) interestFrom = interestTo = aggInterest;

            // main loop
            for( int i1=locationFrom; i1<=locationTo; i1++ )
                for (int i2 = statusFrom; i2 <= statusTo; i2++)
                    for (int i3 = sexFrom; i3 <= sexTo; i3++)
                        for (int i4 = yearFrom; i4 <= yearTo; i4++)
                            for (int i5 = interestFrom; i5 <= interestTo; i5++)
                            {
                                int count = ( counters[i1, i2, i3, i4, i5] );
                                if (count > 0)
                                    visitor(i1, i2, i3, i4, i5, count);
                            }
        }


        public CubeKind Kind { get=>cubeKind;}

        public int Length
        {
            get => maxLocations * maxStatuses * maxSexes * maxYears * maxInterests;
        }

        public int Count
        {
            get
            {
                int count = 0;
                for (int i1 = 0; i1 <= maxLocations; i1++)
                    for (int i2 = 0; i2 < maxStatuses; i2++)
                        for (int i3 = 0; i3 < maxSexes; i3++)
                            for (int i4 = 0; i4 < maxYears; i4++)
                                for (int i5 = 0; i5 < maxInterests; i5++)
                                    if (counters[i1, i2, i3, i4, i5] > 0)
                                        count++;
                return count;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void updateAll(int location, int status, int sex, int year, int i, int diff)
        {
            unchecked
            {
                counters[aggLocation, aggStatus, aggSex, aggYear, i] += diff;
                counters[aggLocation, aggStatus, aggSex, year, i] += diff;
                counters[aggLocation, aggStatus, sex, aggYear, i] += diff;
                counters[aggLocation, aggStatus, sex, year, i] += diff;
                counters[aggLocation, status, aggSex, aggYear, i] += diff;
                counters[aggLocation, status, aggSex, year, i] += diff;
                counters[aggLocation, status, sex, aggYear, i] += diff;
                counters[aggLocation, status, sex, year, i] += diff;
                counters[location, aggStatus, aggSex, aggYear, i] += diff;
                counters[location, aggStatus, aggSex, year, i] += diff;
                counters[location, aggStatus, sex, aggYear, i] += diff;
                counters[location, aggStatus, sex, year, i] += diff;
                counters[location, status, aggSex, aggYear, i] += diff;
                counters[location, status, aggSex, year, i] += diff;
                counters[location, status, sex, aggYear, i] += diff;
                counters[location, status, sex, year, i] += diff;
            }
        }

    }


    public class HypercubeHash : IHypercube
    {
        Dictionary<int,int> counters;

        // aggregate indices
        int aggLocations = 1023;
        int aggStatuses = 0;
        int aggSexes = 0; // 2 bit
        int aggYears = 0; // 6 bit 
        int aggInterests = 0; // 7 bit
        CubeKind cubeKind;

        // modifiers, resolved in the constructor
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Inc(int key)
        {
            if (!counters.TryAdd(key, 1))
                counters[key]++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Dec(int key)
        {
            counters[key]--;
        }

        // constructor
        public HypercubeHash( CubeKind cubeKind, int capacity )
        {
            this.cubeKind = cubeKind;
            counters = new Dictionary<int, int>(capacity);
        }

        public void Reset(CubeKind kind)
        {
            cubeKind = kind;
            counters.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int getKey(int i1, int i2, int i3, int i4, int i5)
        {
            return (i1 << 18) | (i2 << 16) | (i3 << 14) | (i4 << 7) | i5;
        }

        // count in
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Include(int location, int status, int sex, int year, BitMap96 interests)
        {
            updateAll(location, status, sex, year, aggInterests, Inc);
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (interests.IsSet(i))
                    updateAll(location, status, sex, year, i, Inc);
        }

        // count out
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exclude(int location, int status, int sex, int year, BitMap96 interests)
        {
            updateAll(location, status, sex, year, aggInterests, Dec);
            for (int i = 1; i < BitMap96.MAX_BITS; i++)
                if (interests.IsSet(i))
                    updateAll(location, status, sex, year, i, Dec);
        }

        // getter (watch for -1!)
        public int this[int i1, int i2, int i3, int i4, int i5]
        {
            get { return counters[getKey(i1, i2, i3, i4, i5)]; }
        }

        // slice the cube with non-zero counts
        public void Slice(
            int locationFrom, int locationTo,
            int statusFrom, int statusTo,
            int sexFrom, int sexTo,
            int yearFrom, int yearTo,
            int interestFrom, int interestTo,
            CubeVisitor visitor)
        {
            // check for aggregates
            if (locationFrom < 0) locationFrom = locationTo = aggLocations;
            if (statusFrom < 0) statusFrom = statusTo = aggStatuses;
            if (sexFrom < 0) sexFrom = sexTo = aggSexes;
            if (yearFrom < 0) yearFrom = yearTo = aggYears;
            if (interestFrom < 0) interestFrom = interestTo = aggInterests;

            // main loop
            for (int i1 = locationFrom; i1 <= locationTo; i1++)
                for (int i2 = statusFrom; i2 <= statusTo; i2++)
                    for (int i3 = sexFrom; i3 <= sexTo; i3++)
                        for (int i4 = yearFrom; i4 <= yearTo; i4++)
                            for (int i5 = interestFrom; i5 <= interestTo; i5++)
                            {
                                int key = getKey(i1, i2, i3, i4, i5);
                                if (counters.ContainsKey(key))
                                    visitor(i1, i2, i3, i4, i5, counters[key]);
                            }
        }


        public CubeKind Kind { get => cubeKind; }

        public int Count => counters.Count;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void updateAll(int location, int status, int sex, int year, int i, Action<int> updater)
        {
            updater( getKey(aggLocations, aggStatuses, aggSexes, aggYears, i));
            updater( getKey(aggLocations, aggStatuses, aggSexes, year, i));
            updater( getKey(aggLocations, aggStatuses, sex, aggYears, i));
            updater( getKey(aggLocations, aggStatuses, sex, year, i));
            updater( getKey(aggLocations, status, aggSexes, aggYears, i));
            updater( getKey(aggLocations, status, aggSexes, year, i));
            updater( getKey(aggLocations, status, sex, aggYears, i));
            updater( getKey(aggLocations, status, sex, year, i));
            updater( getKey(location, aggStatuses, aggSexes, aggYears, i));
            updater( getKey(location, aggStatuses, aggSexes, year, i));
            updater( getKey(location, aggStatuses, sex, aggYears, i));
            updater( getKey(location, aggStatuses, sex, year, i));
            updater( getKey(location, status, aggSexes, aggYears, i));
            updater( getKey(location, status, aggSexes, year, i));
            updater( getKey(location, status, sex, aggYears, i));
            updater( getKey(location, status, sex, year, i));
        }

    }


}
