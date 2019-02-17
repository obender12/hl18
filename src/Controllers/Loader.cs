using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Utf8Json;

namespace hl18
{
    public static class Loader
    {
        // zip loader, called from Main() before the web server starts
        public static void LoadFromZip(string path, Storage store)
        {
            // load options
            var optionsFile = path + "/data/options.txt";
            if (File.Exists(optionsFile))
            {
                Log.Info("Opening " + optionsFile);
                using (StreamReader r = new StreamReader(File.OpenRead(optionsFile)))
                {
                    store.Now = int.Parse(r.ReadLine());
                    store.IsRatingRun = int.Parse(r.ReadLine());
                }
            }
            else
            {
                Log.Error(optionsFile + " is not found");
            }

            // load zipped data
            var zipFile = path + "/data/data.zip";
            if (File.Exists(zipFile))
            {
                Log.Info("Opening " + zipFile);
                var totalAccounts = 0;
                var errorAccounts = 0;
                var fileBuffer = new byte[20000000];
                using (var file = File.OpenRead(zipFile))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                        if (entry.Name == "options.txt")
                        {
                            Log.Info("Loading options.txt");
                            using (StreamReader r = new StreamReader(entry.Open()))
                            {
                                store.Now = int.Parse(r.ReadLine());
                                store.IsRatingRun = int.Parse(r.ReadLine());
                            }
                        }
                        else
                        {
                            //Log.Info("Loading " + entry.Name);
                            using (var stream = entry.Open())
                            {
                                Console.Write('.');
                                var ms = new MemoryStream(fileBuffer);
                                stream.CopyTo(ms);

                                JsonReader reader = new JsonReader(fileBuffer);
                                if (reader.ReadIsNull() || !reader.ReadIsBeginObject())
                                    throw new Exception("Could not read the init json");

                                if (reader.ReadPropertyName() != "accounts")
                                    throw new Exception("Unexpected object in init json");

                                // read array members
                                if (!reader.ReadIsBeginArray())
                                    throw new Exception("Array of accounts not found");

                                var fileParseStart = Stats.Watch.Elapsed;
                                var dto = DtoAccount.Obtain();
                                while (true)
                                {
                                    if (DtoAccount.Parse(ref reader, dto, store))
                                    {
                                        dto.flags = DtoFlags.Init;
                                        // now, add to the storage the account with internal ids/likes
                                        store.InitNewAccout(dto);

                                        totalAccounts++;
                                        dto.Reset();
                                    }
                                    else
                                        break;
                                    if (!reader.ReadIsValueSeparator())
                                        break;
                                }
                                DtoAccount.Release(dto);
                                var fileParseEnd = Stats.Watch.Elapsed;
                                if ((fileParseEnd - fileParseStart).TotalSeconds > 1)
                                    Garbage.CollectAll();
                                /*
                                else
                                    Garbage.Collect0();*/
                            }
                        }
                }
                Log.Info("Total accounts loaded: " + totalAccounts + ", " + errorAccounts + " errors found");
                fileBuffer = null;
            }
            else
            {
                Log.Error(zipFile + " not found");
            }
        }


        // recursively load all of assemblies referenced by the given assembly
        public static void ForceLoadAllAssembliesAndPreJIT()
        {
            var current = Assembly.GetExecutingAssembly();
            var assemblies = new HashSet<Assembly>();
            ForceLoadAll(current, assemblies);
        }

        private static void ForceLoadAll(Assembly assembly,
                                         HashSet<Assembly> loadedAssemblies)
        {
            bool alreadyLoaded = !loadedAssemblies.Add(assembly);
            if (alreadyLoaded)
                return;

            AssemblyName[] refrencedAssemblies =
                assembly.GetReferencedAssemblies();

            foreach (AssemblyName curAssemblyName in refrencedAssemblies)
            {
                Assembly nextAssembly = Assembly.Load(curAssemblyName);
                if (nextAssembly.GlobalAssemblyCache)
                    continue;

                ForceLoadAll(nextAssembly, loadedAssemblies);
            }
        }
    }
}
