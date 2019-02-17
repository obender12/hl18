using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace hl18
{
    public struct Overspan
    {
        private readonly TimeSpan startTime;
        /*
        private readonly long startMem;
        private readonly int startOpenedSockets;
        private readonly int startClosedSockets;
        */
        private readonly int interval;

        public Overspan(int interval)
        {
            startTime = Stats.Watch.Elapsed;
            /*
            startMem = GC.GetTotalMemory(false);
            startOpenedSockets = EpollHandler.OpenedSockets;
            startClosedSockets = EpollHandler.ClosedSockets;*/
            this.interval = interval;
        }

        public void Check(string name)
        {
            var now = Stats.Watch.Elapsed;
            if( (now-startTime).TotalMilliseconds > interval )
            {
                /*
                Log.Warning("{7} overspan {0} before={1}/{2}/{3} after={4}/{5}/{6}",
                    (now - startTime), startMem/1024/1024, startOpenedSockets, startClosedSockets,
                    GC.GetTotalMemory(false) / 1024 / 1024, EpollHandler.OpenedSockets, EpollHandler.ClosedSockets, name);*/
                Log.Warning("{0} overspan {1}", name, now - startTime);
            }
        }
    }

    public class Stats
    {
        public static Stopwatch Watch = new Stopwatch();
        
        //constructor
        static Stats()
        {
            Watch.Start();
        }


        private static ConcurrentDictionary<string, TimeSpan> timeStats = new ConcurrentDictionary<string, TimeSpan>();
        private static TimeSpan totalTime = new TimeSpan();

        [Conditional("STATS")]
        public static void ReportContextTime(string reqType, TimeSpan elapsed )
        {
            totalTime += elapsed;
            if (timeStats.TryGetValue(reqType, out var span))
                timeStats[reqType] = span.Add(elapsed);
            else
                timeStats.TryAdd(reqType, elapsed);
        }

        [Conditional("STATS")]
        public static void DisplayContextTime()
        {
            Console.WriteLine("Total time: {0}", totalTime);
            var maxTop = 10;
            foreach (var kv in timeStats.Select(x => x).OrderByDescending(x => x.Value))
            {
                Console.WriteLine("{0}: {1}", kv.Key, kv.Value);
                if (--maxTop <= 0)
                    break;
            }
        }
    }
}
