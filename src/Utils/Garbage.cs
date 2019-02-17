using System;
using System.Runtime;
using System.Threading;

namespace hl18
{
    public class Garbage
    {
        static Garbage()
        {
            // suppress gen2 gc until explicitely told
            Log.Info("IsServerGC: {0}, LatencyMode: {1}", GCSettings.IsServerGC, GCSettings.LatencyMode);
        }

        public static void CollectAll(bool log = true)
        {
            long mem1 = GC.GetTotalMemory(false);

            // stop the world
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete(); 
            GC.Collect(2, GCCollectionMode.Forced, true);

            // restore the non-intrusive gc
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            if (log)
            {
                long mem2 = GC.GetTotalMemory(false);
                Log.Info("Garbage collected {0} => {1}, LatencyMode: {2}", mem1, mem2, GCSettings.LatencyMode);
            }
        }
        
        public static void Collect0()
        {
            GC.Collect(0, GCCollectionMode.Optimized);
        }

        private static int noGcCounter = 0;
        public static void EnterNoGC()
        {
            Interlocked.Increment(ref noGcCounter);
            if (GCSettings.LatencyMode != GCLatencyMode.NoGCRegion)
                try
                {
                    GC.TryStartNoGCRegion(64 * 1024 * 1024, true);
                }
                catch (Exception)
                {
                    // already in NoGC mode
                }
        }

        public static void ExitNoGC()
        {
            if (noGcCounter > 0 && Interlocked.Decrement(ref noGcCounter) <= 0 && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
                GC.Collect(0);
            }
        }
    }
}
