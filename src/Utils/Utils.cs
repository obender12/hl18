using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hl18
{
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
                return left == right;
            return left.SequenceEqual(right);
        }

        public int GetHashCode(byte[] array)
        {
            if (array == null || array.Length == 0)
                return 0;
            int hc = array.Length;
            for (int i = 0; i < array.Length; ++i)
                hc = unchecked(hc * 314159 + array[i]);
            return hc;
        }

        public static ByteArrayComparer Instance = new ByteArrayComparer();
    }

    public class ReverseComparer : Comparer<int>
    {
        public override int Compare(int x, int y) => y.CompareTo(x);

        public static ReverseComparer Instance = new ReverseComparer();
    }

    public static class Utils
    {
        // convert unix timestamp into a year
        public static DateTime TimestampToDate(int timeStamp) => Epoch.AddSeconds(timeStamp);
        static DateTime Epoch = new DateTime(1970, 1, 1);

        public static int ToUnix(this DateTime d) => (int)(d - Epoch).TotalSeconds;

        // compare two byte arrays
        public static int CompareTo(this byte[] x, byte[] y)
        {
            // simple cases
            /*
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            */
            // get the 2 lengths and the minimum
            int len = x.Length < y.Length ? x.Length : y.Length;
            // loop and test
            for (int i = 0; i < len; i++)
            {
                if (x[i] < y[i])
                    return -1;
                if (x[i] > y[i])
                    return 1;
            }
            return x.Length.CompareTo(y.Length);
        }

        public static bool EqualTo(this ArraySegment<byte> x, ArraySegment<byte> y) 
        {
            if (x.Count != y.Count)
                return false;
            for( int i=0; i<x.Count; i++ )
                if (x[i] != y[i])
                    return false;
            return true;
        }


        // no allocations
        public static bool FindIntegerAfter(this string source, string template, out int index)
        {
            index = 0;
            var start = source.IndexOf(template, StringComparison.InvariantCultureIgnoreCase);
            if (start < 0)
                return false; // could not find the template
            start += template.Length;

            // skip to the first digit
            while (start < source.Length && 
                (source[start] < '0' || source[start] > '9') && 
                source[start] != '-')
                start++;
            if (start == source.Length)
                return false;

            // find the end of the number
            var end = start;
            while (end < source.Length && 
                (source[end] >= '0' && source[end] <= '9' || 
                source[end] == '-'))
                end++;

            // try converting to int
            return int.TryParse(source.AsSpan().Slice(start, end - start), out index);
        }


        // linear search in the sorted array
        public static int LinearSearch<T>(this List<T> list, T item, IComparer<T> comparer)
        {
            for( int i=0; i<list.Count; i++ )
            {
                var c = comparer.Compare(list[i], item);
                if (c == 0)
                    return i;
                if (c > 0)
                    return ~i;
            }
            return ~list.Count;
        }
    }

}
