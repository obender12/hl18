using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace hl18
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var appStarted = Stats.Watch.Elapsed;
            Log.Info("Version 1.0rc");
            Garbage.Collect0(); // just to reference Garbage static constructor

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

            // path to data
            var path = "/tmp";
            if (args.Length > 0)
                path = args[0];

            // create the storage and load initial dataset
            var store = new Storage();
            Loader.LoadFromZip(path, store);
            Garbage.CollectAll(true);

            // prepare the storage and clean up memory before the start
            store.Prepare(true);
            Garbage.CollectAll(true);

            // create the router and the house keeper
            var router = new Router(store);

            // warmup the solver
            if( store.IsRatingRun==1 )
            {
                var warmupDeadline = new TimeSpan(0, 3, 0);
                var warmer = new Warmup(router, store);
                Log.Info("Warm up the processors before simulation start");
                warmer.RunGet(Stats.Watch.Elapsed.Add(new System.TimeSpan(0, 0, 5)));
                Log.Info("Warm up completed");
                Garbage.CollectAll();
            }

            // start the Epoll server (only on Linux)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                new EpollServer(router).Run();
                Console.ReadKey();
            }

            // local run
            if (Directory.Exists(path + "/ammo"))
                // local simulation
                new Tester(store, router).TestAll(path);

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

    }



}
