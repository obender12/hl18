using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hl18
{
    public interface IRange
    {
        string Name { get; set; }
        AString AName { get; set; }
        int Index { get; set; }
        int Count { get; }
        bool Contains(int i);
        void Include(int i);
        void Exclude(int i);
        void Prepare();
        IEnumerable<int> Enumerate();
    }

    // empty range implementation
    public class EmptyRange : IRange
    {
        public string Name { get; set; }
        public AString AName { get; set; }
        public int Index { get; set; }
        public int Count => 0;
        public bool Contains(int i) => false;
        public IEnumerable<int> Enumerate() => Enumerable.Empty<int>();
        public void Include(int i) { }
        public void Exclude(int i) { }
        public void Prepare() { }
        public static IRange Empty = new EmptyRange();
    }

    // counter only range implementation
    public class CounterRange : IRange
    {
        private int count = 0;
        public string Name { get; set; }
        public AString AName { get; set; }
        public int Index { get; set; }
        public int Count => count;
        public bool Contains(int i) => false;
        public IEnumerable<int> Enumerate() => Enumerable.Empty<int>();
        public void Include(int i) { count++; }
        public void Exclude(int i) { count--; }
        public void Prepare() { }
    }
    
    // a collection of named ranges, accessed by name or by index
    public class RangeBag<K>: IEnumerable<IRange> where K: IEquatable<K>
    {
        protected Dictionary<K, IRange> nDict = new Dictionary<K, IRange>();
        protected List<IRange> nList = new List<IRange>();

        public RangeBag()
        {
            nList.Add(EmptyRange.Empty);
        }

        public bool Contains(K key)
        {
            return nDict.ContainsKey(key);
        }

        public bool TryGetValue(K key, out IRange r)
        {
            return nDict.TryGetValue(key, out r);
        }

        public void Add(K key, IRange range)
        {
            range.Name = key.ToString();
            range.AName = new AString( Encoding.UTF8.GetBytes(range.Name) );
            range.Index = nList.Count;
            if (key is ICloneable)
                nDict.Add((K)(key as ICloneable).Clone(), range);
            else
                nDict.Add(key, range);
            nList.Add(range);
        }

        // helper: finds or creates a new range, includes the specified id, and returns the range's index
        public IRange GetOrCreateRangeThenInclude(K key, int id)
        {
            if (!TryGetValue(key, out IRange index))
                Add(key, index = CreateRange() );
            index.Include(id);
            return index;
        }

        // helper: finds or creates a new range
        public IRange GetOrCreateRange(K key)
        {
            if (!TryGetValue(key, out IRange index))
                Add(key, index = CreateRange() );
            return index;
        }

        // range factory
        public virtual IRange CreateRange() => EmptyRange.Empty;

        public IRange this[int index]
        {
            get { return nList[index]; }
        }

        public void Prepare()
        {
            foreach (var b in nList)
                b.Prepare();
        }

        public int RefreshBitmaps()
        {
            var rval = 0;
            foreach (var b in nList.ToArray())
                if (b is BitMap)
                    rval += (b as BitMap).UpdateCountCache();
            return rval;
        }

        public IEnumerator<IRange> GetEnumerator() => nList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => nList.GetEnumerator();

        public int Count { get { return nList.Count; } }
    }

    // generic class range bag
    public class RangeBagOf<K, V>: RangeBag<K> 
        where K: IEquatable<K> 
        where V: IRange, new()
    {
        public RangeBagOf(int capacity = 0)
        : base()
        {
            nDict.EnsureCapacity(capacity);
            nList.Capacity = capacity;
        }
        // range factory
        public override IRange CreateRange() => new V();
    }
}
