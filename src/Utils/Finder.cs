using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace hl18
{
    public delegate bool FinderDelegate(int id);
    public unsafe class Finder
    {
        private static int MAX_AND_BITMAPS = 10;
        private static int MAX_OR_BITMAPS = 10;
        private static int MAX_CONDITIONS = 4;
        private static BitMap emptyBitmap = new BitMap(Storage.MAX_ACCOUNTS);
        private static int max_count = BitMap.GetInt64ArrayLengthFromBitLength(Storage.MAX_ACCOUNTS);
        public BitMap DefaultBitmap;
        
        // constructor
        public Finder(BitMap defaultBitmap)
        {
            DefaultBitmap = defaultBitmap;
        }

        // copy constructor
        public Finder(Finder source)
        {
            DefaultBitmap = source.DefaultBitmap;
            andBitmapCount = source.andBitmapCount;
            for (int i = 0; i < andBitmapCount; i++)
                AndBitmaps[i] = source.AndBitmaps[i];
            orBitmapCount = source.orBitmapCount;
            for (int i = 0; i < orBitmapCount; i++)
                OrBitmaps[i] = source.OrBitmaps[i];
            orBitmapCount2 = source.orBitmapCount2;
            for (int i = 0; i < orBitmapCount2; i++)
                OrBitmaps2[i] = source.OrBitmaps2[i];
            conditions.AddRange(source.conditions);
        }

        // reset (for reuse)
        public void Reset(BitMap defaultBitmap)
        {
            DefaultBitmap = defaultBitmap;
            for (int i = 0; i < MAX_AND_BITMAPS; i++)
                AndBitmaps[i] = null;
            for (int i = 0; i < MAX_OR_BITMAPS; i++)
                OrBitmaps[i] = OrBitmaps2[i] = null;
            andBitmapCount = orBitmapCount = orBitmapCount2 = 0;
            conditions.Clear();
        }

        #region AND bitmap group

        // add a bitmap to the 'and' group
        public void AndBitmap(BitMap bitMap)
        {
            if (andBitmapCount == MAX_AND_BITMAPS)
            {
                Log.Error("Too many AND-bitmaps in Finder");
                return;
            }
            if (bitMap == null)
                DefaultBitmap = null;
            else
                AndBitmaps[andBitmapCount++] = bitMap;
        }

        // replace a bitmap from the 'and' group
        public void SetBitmap(int index, BitMap bitMap)
        {
            AndBitmaps[index] = bitMap;
        }

        private int andBitmapCount;
        public BitMap[] AndBitmaps = new BitMap[MAX_AND_BITMAPS];
        public BitMap[] AndBitmapsSorted = new BitMap[MAX_AND_BITMAPS];
        public int AndBitmapCount { get => andBitmapCount; }
        private long* and0;
        private long* and1;
        private long* and2;
        private long* and3;
        private long* and4;
        private long* and5;
        private long* and6;
        private long* and7;
        private long* and8;
        private long* and9;

        private void prepareAndBitmap(int index, BitMap bitMap)
        {
            fixed ( long* p = &bitMap.m_array[0] )
                switch (index)
                {
                    case 0: and0 = p; break;
                    case 1: and1 = p; break;
                    case 2: and2 = p; break;
                    case 3: and3 = p; break;
                    case 4: and4 = p; break;
                    case 5: and5 = p; break;
                    case 6: and6 = p; break;
                    case 7: and7 = p; break;
                    case 8: and8 = p; break;
                    case 9: and9 = p; break;
                }
        }

        #endregion

        #region OR bitmap groups

        public void OrBitmap(int group, BitMap bitMap)
        {
            if (group == 0)
            {
                if (orBitmapCount == MAX_OR_BITMAPS)
                {
                    Log.Error("Too many OR(0)-bitmaps in Finder");
                    return;
                }
                else
                    OrBitmaps[orBitmapCount++] = bitMap;
            }
            else
            if (group == 1)
            {
                if (orBitmapCount2 == MAX_OR_BITMAPS)
                {
                    Log.Error("Too many OR(1)-bitmaps in Finder");
                    return;
                }
                else
                    OrBitmaps2[orBitmapCount2++] = bitMap;
            }
            else
                Log.Error("Wrong or-bitmap group: " + group);
        }
        private BitMap[] OrBitmaps = new BitMap[MAX_OR_BITMAPS];
        private int orBitmapCount;
        private long* or0;
        private long* or1;
        private long* or2;
        private long* or3;
        private long* or4;
        private long* or5;
        private long* or6;
        private long* or7;
        private long* or8;
        private long* or9;

        private void prepareOrBitmap(int index, BitMap bitMap)
        {
            fixed (long* p = &bitMap.m_array[0])
                switch (index)
                {
                    case 0: or0 = p; break;
                    case 1: or1 = p; break;
                    case 2: or2 = p; break;
                    case 3: or3 = p; break;
                    case 4: or4 = p; break;
                    case 5: or5 = p; break;
                    case 6: or6 = p; break;
                    case 7: or7 = p; break;
                    case 8: or8 = p; break;
                    case 9: or9 = p; break;
                }
        }

        private BitMap[] OrBitmaps2 = new BitMap[MAX_OR_BITMAPS];
        private int orBitmapCount2;
        private long* or0_;
        private long* or1_;
        private long* or2_;
        private long* or3_;
        private long* or4_;
        private long* or5_;
        private long* or6_;
        private long* or7_;
        private long* or8_;
        private long* or9_;

        private void prepareOrBitmap2(int index, BitMap bitMap)
        {
            fixed (long* p = &bitMap.m_array[0])
                switch (index)
                {
                    case 0: or0_ = p; break;
                    case 1: or1_ = p; break;
                    case 2: or2_ = p; break;
                    case 3: or3_ = p; break;
                    case 4: or4_ = p; break;
                    case 5: or5_ = p; break;
                    case 6: or6_ = p; break;
                    case 7: or7_ = p; break;
                    case 8: or8_ = p; break;
                    case 9: or9_ = p; break;
                }
        }

        #endregion

        #region Conditions groups

        struct FinderCondition
        {
            public FinderDelegate Condition;
            public int Priority;
        }
        private List<FinderCondition> conditions = new List<FinderCondition>();
        private FinderDelegate cond0;
        private FinderDelegate cond1;
        private FinderDelegate cond2;
        private FinderDelegate cond3;

        // add a condition to the finder
        public void AddCondition(FinderDelegate condition, int priority)
        {
            if( conditions.Count==MAX_CONDITIONS )
            {
                Log.Error("Too many conditions in Finder");
                return;
            }
            conditions.Add(new FinderCondition { Condition = condition, Priority = priority });
        }

        private void prepareCondition(int index, FinderDelegate cond)
        {
            switch (index)
            {
                case 0: cond0 = cond; break;
                case 1: cond1 = cond; break;
                case 2: cond2 = cond; break;
                case 3: cond3 = cond; break;
            }
        }

        public bool Check(int id)
        {
            var idx = Storage.MAX_ACCOUNTS - id;
            // first, check in and-maps
            for (int i = 0; i < andBitmapCount; i++)
            {
                if (!getValue(idx, AndBitmaps[i].m_array))
                    return false;
            }
            // then, check in or-maps
            if (orBitmapCount > 0)
            {
                bool orVal = false;
                for (int i = 0; i < orBitmapCount; i++)
                    orVal = orVal || getValue(idx, OrBitmaps[i].m_array);
                if (!orVal)
                    return false;
            }
            // then, check in or2-maps
            if (orBitmapCount2 > 0)
            {
                bool orVal = false;
                for (int i = 0; i < orBitmapCount2; i++)
                    orVal = orVal || getValue(idx, OrBitmaps[i].m_array);
                if (!orVal)
                    return false;
            }
            // finally, check conditions
            for (int i = 0; i < conditions.Count; i++)
                if (!conditions[i].Condition(id))
                    return false;
            // everything checks out
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool getValue(int id, long[] array)
        {
            var by = id >> 6;
            var bi = id & 63;
            return (unchecked(array[by] & (1L << bi))) != 0;
        }

        #endregion

        #region Prepare and Find

        //private int minIndex = 0; // max range by default
        //private int maxIndex = Storage.MAX_ACCOUNTS-1;

        public bool Prepare()
        {
            if (DefaultBitmap == null)
                return false; // no sense to find

            if (andBitmapCount == 0)
                AndBitmap(DefaultBitmap);

            /* there is a bug with maxIndex, turning off for now

            // narrow the range from And-bitmaps
            minIndex = 0;
            maxIndex = AndBitmaps[0].m_array.Length-1;
            
            // for now only for and bitmaps
            for (int i = 0; i < andBitmapCount; i++)
            {
                if (AndBitmaps[i].MinOne > minIndex)
                    minIndex = AndBitmaps[i].MinOne;
                if (AndBitmaps[i].MaxOne < maxIndex)
                    maxIndex = AndBitmaps[i].MaxOne;
            }
            */


            // sort and-bitmaps by bit count
            Array.Copy(AndBitmaps, AndBitmapsSorted, andBitmapCount);
            Array.Sort(AndBitmapsSorted, 0, andBitmapCount);

            // sort conditions by priority
            conditions.Sort((x, y) => x.Priority.CompareTo(y.Priority));

            // prepare the pointers
            for (int i = 0; i < andBitmapCount; i++)
                prepareAndBitmap(i, AndBitmapsSorted[i]);
            for (int i = 0; i < orBitmapCount; i++)
                prepareOrBitmap(i, OrBitmaps[i]);
            for (int i = 0; i < orBitmapCount2; i++)
                prepareOrBitmap2(i, OrBitmaps2[i]);
            for (int i = 0; i < conditions.Count; i++)
                prepareCondition(i, conditions[i].Condition);

            return true;
        }


        // !! prepare first!
        public int Find(int maxIDs, FinderDelegate result)
        {
            // traverse the bitmaps
            int found = 0;
            var condCount = conditions.Count;
            for (int by = 0; by<max_count; by++)
            {
                long mask = and0[by];
                if (mask == 0)
                    continue; // shortcut, it really helps, since we start with the least dense bitmap

                // so many conditions inside a tight loop? crazy?
                // - the CPU is pretty good at branch predicting effectively, 
                //   since these conditions are the same across the whole finder loop

                if (andBitmapCount > 1)
                {
                    mask &= and1[by];
                    if (mask == 0) continue;
                    if (andBitmapCount > 2)
                    {
                        mask &= and2[by];
                        if (mask == 0) continue;
                        if (andBitmapCount > 3)
                        {
                            mask &= and3[by];
                            if (mask == 0) continue;
                            if (andBitmapCount > 4)
                            {
                                mask &= and4[by];
                                if (mask == 0) continue;
                                if (andBitmapCount > 5)
                                {
                                    mask &= and5[by];
                                    if (mask == 0) continue;
                                    if (andBitmapCount > 6)
                                    {
                                        mask &= and6[by];
                                        if (mask == 0) continue;
                                        if (andBitmapCount > 7)
                                        {
                                            mask &= and7[by];
                                            if (mask == 0) continue;
                                            if (andBitmapCount > 8)
                                            {
                                                mask &= and8[by];
                                                if (mask == 0) continue;
                                                if (andBitmapCount > 9)
                                                    mask &= and9[by];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (orBitmapCount > 0)
                {
                    var ormask = or0[by];
                    if (orBitmapCount > 1)
                    {
                        ormask |= or1[by];
                        if (orBitmapCount > 2)
                        {
                            ormask |= or2[by];
                            if (orBitmapCount > 3)
                            {
                                ormask |= or3[by];
                                if (orBitmapCount > 4)
                                {
                                    ormask |= or4[by];
                                    if (orBitmapCount > 5)
                                    {
                                        ormask |= or5[by];
                                        if (orBitmapCount > 6)
                                        {
                                            ormask |= or6[by];
                                            if (orBitmapCount > 7)
                                            {
                                                ormask |= or7[by];
                                                if (orBitmapCount > 8)
                                                {
                                                    ormask |= or8[by];
                                                    if (orBitmapCount > 9)
                                                        ormask |= or9[by];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    mask &= ormask;

                    if (orBitmapCount2 > 0)
                    {
                        ormask = or0_[by];
                        if (orBitmapCount2 > 1)
                        {
                            ormask |= or1_[by];
                            if (orBitmapCount2 > 2)
                            {
                                ormask |= or2_[by];
                                if (orBitmapCount2 > 3)
                                {
                                    ormask |= or3_[by];
                                    if (orBitmapCount2 > 4)
                                    {
                                        ormask |= or4_[by];
                                        if (orBitmapCount2 > 5)
                                        {
                                            ormask |= or5_[by];
                                            if (orBitmapCount2 > 6)
                                            {
                                                ormask |= or6_[by];
                                                if (orBitmapCount2 > 7)
                                                {
                                                    ormask |= or7_[by];
                                                    if (orBitmapCount2 > 8)
                                                    {
                                                        ormask |= or8_[by];
                                                        if (orBitmapCount2 > 9)
                                                            ormask |= or9_[by];
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        mask &= ormask;
                        if (mask == 0) continue;
                    }
                }


                if (mask != 0)
                    for (int i = 0; i < 64; i++)
                        if (unchecked(mask & (1L << i)) != 0)
                        {
                            var id = Storage.MAX_ACCOUNTS - ((by << 6) + i);
                            bool cond = true;
                            if( condCount>0 )
                            {
                                cond &= cond0(id);
                                if (!cond) continue;
                                if( condCount>1 )
                                {
                                    cond &= cond1(id);
                                    if (!cond) continue;
                                    if (condCount > 2)
                                    {
                                        cond &= cond2(id);
                                        if (!cond) continue;
                                        if (condCount > 3)
                                        {
                                            cond &= cond3(id);
                                            if (!cond) continue;
                                        }
                                    }
                                }
                            }

                            if (!result(id))
                                return found;
                            if (++found >= maxIDs)
                                return found;
                        }
            }
            return found;
        }

        #endregion
    }

}
