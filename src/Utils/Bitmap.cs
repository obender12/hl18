using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace hl18
{
    // adapted from Microsoft's BitVector
    public class BitMap : IRange, IComparable<BitMap>
    {
        // IRange implementation
        public string Name { get; set; }
        public AString AName { get; set; }
        public int Index { get; set; }
        public bool Contains(int i) => this[i];
        public void Include(int i) => Set(i, true);
        public void Exclude(int i) => Set(i, false);
        public void Prepare() => UpdateCountCache();
        public int Count
        {
            get
            {
                if (cachedCount < 0)
                    UpdateCountCache();
                return cachedCount;
            }
        }
        public int MinOne { get => minOne; }
        public int MaxOne { get => maxOne; }

        // enumerate backwards
        public IEnumerable<int> Enumerate()
        {
            for( int word=0; word<m_array.Length; word++ )
            {
                var val = m_array[word];
                if (val != 0)
                    for (int bi = 0; bi < 64; bi++)
                    {
                        if (unchecked (val & (1<<bi)) != 0 )
                            yield return m_length-( (word<<6) | bi);
                    }
            }
            yield break;
        }


        // Bitmap implementation
        public long[] m_array;
        public int m_length;

        public BitMap(int length)
            : this(length, false)
        {
            cachedCount = 0;
        }

        public BitMap(int length, bool defaultValue)
        {
            m_array = new long[GetInt64ArrayLengthFromBitLength(length)];
            m_length = length;
            if (defaultValue)
                for (int i = 0; i < m_array.Length; i++)
                    m_array[i] = -1L;
            cachedCount = defaultValue ? length : 0;
        }

        public BitMap(long[] values)
        {
            m_array = new long[values.Length];
            Array.Copy(values, 0, m_array, 0, values.Length);
            m_length = values.Length * BitsPerInt64;
        }

        public BitMap(BitMap bits)
        {
            int arrayLength = GetInt64ArrayLengthFromBitLength(bits.m_length);
            m_array = new long[arrayLength];
            Array.Copy(bits.m_array, 0, m_array, 0, arrayLength);
            m_length = bits.m_length;
        }

        public bool this[int index]
        {
            get { return Get(index); }
            set { Set(index, value); }
        }

        public bool Get(int index)
        {
            index = m_length - index;
            int elementIndex = Div64Rem(index, out int extraBits);
            return (m_array[elementIndex] & (1L << extraBits)) != 0;
        }

        public void Set(int index, bool value)
        {
            index = m_length - index;
            int elementIndex = Div64Rem(index, out int extraBits);
            long newValue = m_array[elementIndex];
            if (value)
                newValue |= 1L << extraBits;
            else
                newValue &= ~(1L << extraBits);
            m_array[elementIndex] = newValue;
            cachedCount = -1;
        }

        public void SetAll(bool value)
        {
            long fillValue = value ? -1L : 0L;
            for (int i = 0; i < m_array.Length; i++)
                m_array[i] = fillValue;
            cachedCount = value ? m_length : 0;
        }

        // Check for any overlap between current and other bitmap
        // Note: has to be aligned to 64 bit
        public unsafe bool Any(BitMap value)
        {
            for (int i = 0; i < m_array.Length; i++)
                if ((m_array[i] & value.m_array[i]) != 0)
                    return true;
            return false;
        }

        // Check that all 1's from 'value' bitmap are present in this bitmap
        // Note: has to be aligned to 64 bit
        public unsafe bool All(BitMap value)
        {
            for (int i = 0; i < m_array.Length; i++)
                if ((m_array[i] & value.m_array[i]) != value.m_array[i])
                    return false;
            return true;
        }

        public unsafe BitMap And(BitMap value)
        {
            int count = m_array.Length;

            int i = 0;
            for (; i < count; i++)
                m_array[i] &= value.m_array[i];
            cachedCount = -1;
            return this;
        }

        public unsafe BitMap Or(BitMap value)
        {
            int count = m_array.Length;
            int i = 0;
            for (; i < count; i++)
                m_array[i] |= value.m_array[i];
            cachedCount = -1;
            return this;
        }

        public unsafe BitMap Xor(BitMap value)
        {
            int count = m_array.Length;
            int i = 0;
            for (; i < count; i++)
                m_array[i] ^= value.m_array[i];
            cachedCount = -1;
            return this;
        }

        public BitMap Not()
        {
            for (int i = 0; i < m_array.Length; i++)
                m_array[i] = ~m_array[i];
            cachedCount = -1;
            return this;
        }

        public unsafe BitMap From(BitMap value)
        {
            for (int i = 0; i < m_array.Length; i++)
                m_array[i] = value.m_array[i];
            cachedCount = -1;
            return this;
        }

        public unsafe BitMap NotFrom(BitMap value)
        {
            int count = m_array.Length;
            int i = 0;
            for (; i < count; i++)
                m_array[i] = ~value.m_array[i];
            cachedCount = -1;
            return this;
        }

        public int Length
        {
            get
            {
                return m_length;
            }
        }

        public void CopyTo(Array array, int index)
        {
            if (array is long[] intArray)
            {
                Div64Rem(m_length, out int extraBits);
                Array.Copy(m_array, 0, intArray, index, m_array.Length);
            }
        }

        private int cachedCount = -1;
        private int minOne = 0;
        private int maxOne = -1;
        public int UpdateCountCache()
        {
            // fix for not truncated bits in last integer that may have been set to true with SetAll()
            cachedCount = 0;
            minOne = maxOne = 0;
            for (int i = 0; i < m_array.Length; i++)
                if (m_array[i] != 0)
                {
                    if (minOne==0)
                        minOne = i;
                    maxOne = i;
                    cachedCount += CountBits64(m_array[i]);
                }
            return cachedCount;
        }

        public object Clone() => new BitMap(this);

        // XPerY=n means that n Xs can be stored in 1 Y. 
        private const int BitsPerInt64 = 64;
        private const int BytesPerInt64 = 8;
        private const int BitsPerByte = 8;

        private const int BitShiftPerInt64 = 6;
        private const int BitShiftPerByte = 3;
        private const int BitShiftForBytesPerInt64 = 3;

        public static int GetInt64ArrayLengthFromBitLength(int n)
        {
            return (int)((uint)(n - 1 + (1L << BitShiftPerInt64)) >> BitShiftPerInt64);
        }

        private static int GetInt64ArrayLengthFromByteLength(int n)
        {
            return (int)((uint)(n - 1 + (1L << BitShiftForBytesPerInt64)) >> BitShiftForBytesPerInt64);
        }

        private static int Div64Rem(int number, out int remainder)
        {
            uint quotient = (uint)number / 64;
            remainder = number & (64 - 1);    // equivalent to number % 32, since 32 is a power of 2
            return (int)quotient;
        }

        public static int CountBits32(Int32 c)
        {
            // magic (http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel)
            unchecked
            {
                c = c - ((c >> 1) & 0x55555555);
                c = (c & 0x33333333) + ((c >> 2) & 0x33333333);
                c = ((c + (c >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
            }
            return c;
        }

        public static int CountBits64(long c)
        {
            var v = (UInt64)c;
            const UInt64 MaskMult = 0x0101010101010101;
            const UInt64 mask1h = (~0UL) / 3 << 1;
            const UInt64 mask2l = (~0UL) / 5;
            const UInt64 mask4l = (~0UL) / 17;
            v -= (mask1h & v) >> 1;
            v = (v & mask2l) + ((v >> 2) & mask2l);
            v += v >> 4;
            v &= mask4l;
            return (int)((v * MaskMult) >> 56);
        }


        /* BitMap pool */
        private static ConcurrentBag<BitMap> bag = new ConcurrentBag<BitMap>();
        public static BitMap Obtain()
        {
            if (!bag.TryTake(out var obj))
                obj = new BitMap(Storage.MAX_ACCOUNTS);
            //else
            //    obj.SetAll(false);
            return obj;
        }
        public static void Release(BitMap obj)
        {
            bag.Add(obj);
        }

        public int CompareTo(BitMap other)
        {
            return Count.CompareTo(other.Count);
        }
    }

    public class BitmapBag<K>: RangeBag<K> where K: IEquatable<K>
    {
        private int size;
        public BitmapBag(int size) => this.size = size;
        public override IRange CreateRange() => new BitMap(size);
    }

    // smaller cousine for 96-bit bitmaps
    public struct BitMap96
    {
        public static int MAX_BITS = 96;

        private long low;
        private int high;

        // set the bit
        public void Set(int i)
        {
            if (i < 64)
                low |= (1L << i);
            else
                high |= (1 << (i-64));
        }

        // reset the bit
        public void Reset(int i)
        {
            if (i < 64)
                low &= ~(1L << i);
            else
                high &= ~(1 << (i-64));
        }

        // check if bit is set
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSet(int i)
        {
            return i < 64 ?
                (low & (1L << i)) != 0 :
                (high & (1 << (i-64))) != 0;
        }

        // all bits from the other bitmask are present
        public bool All(BitMap96 from)
        {
            return (high & from.high) == from.high &&
                (low & from.low) == from.low;
        }

        // any bits from the other bitmask are present
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Any(BitMap96 from)
        {
            return unchecked((high & from.high) + ((long)low & (long)from.low) != 0);
        }

        // intersect with another bitmask
        public void And(BitMap96 other)
        {
            high &= other.high;
            low &= other.low;
        }

        // union with another bitmask
        public void Or(BitMap96 other)
        {
            high |= other.high;
            low |= other.low;
        }

        // count the number of bits
        public int Count
        {
            get =>  BitMap.CountBits32(high) +
                    BitMap.CountBits32((int)(low >> 32)) +
                    BitMap.CountBits32((int)low);
        }

        public static int Common(BitMap96 bm1, BitMap96 bm2)
        {
            long low = bm1.low & bm2.low;
            int high = bm1.high & bm2.high;
            return
                BitMap.CountBits32(high) +
                BitMap.CountBits32((int)(low >> 32)) +
                BitMap.CountBits32((int)low);
        }

        static BitMap96() // self test
        {
            var b = new BitMap96();
            b.Set(74);
            if (!b.IsSet(74)) throw new Exception();
            b.Set(3);
            if (!b.IsSet(3)) throw new Exception();
            b.Reset(3);
            if (b.IsSet(3)) throw new Exception();
        }
    }

}
