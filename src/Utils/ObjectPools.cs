using System;
using System.Collections.Concurrent;
using System.Text;

namespace hl18
{

    public class Pool<T>
    {
        private static ConcurrentBag<T> bag = new ConcurrentBag<T>();
        public static bool TryGet( out T obj)
        {
            return bag.TryTake(out obj);
        }
        public static void Release(T obj)
        {
            bag.Add(obj);
        }
    }


}

