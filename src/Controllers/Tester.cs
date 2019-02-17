using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utf8Json;

namespace hl18
{
    public class Tester
    {
        private readonly Storage store;
        private readonly Router router;
        public Tester(Storage s, Router r)
        {
            store = s;
            router = r;
        }

        public TimeSpan TestAll(string path)
        {
            bool verify = false;

            Console.WriteLine("Test Stage 1");
            var elapsed1 = TestPhase(1, path, verify);
            Console.WriteLine("Test Stage 1 completed in {0}", elapsed1);
            GetGroup.CachedResults.Clear();
            GetFilter.CachedResults.Clear();
            Garbage.CollectAll();

            Console.WriteLine("Test Stage 2");
            var MAX_PHASE2 = verify? 1: 20;
            var elapsed2 = new TimeSpan();
            for (int i = 1; i <= MAX_PHASE2; i++)
            {
                elapsed2 += TestPhase(2, path, verify);
            }
            elapsed2 /= MAX_PHASE2;
            Console.WriteLine("Test Stage 2 completed in {0}", elapsed2);
            Garbage.CollectAll();
            store.Prepare(false);

            var elapsed3 = new TimeSpan();
            var MAX_PHASE3 = 1;
            for( int i=1; i<= MAX_PHASE3; i++)
            {
                Garbage.CollectAll();
                Console.WriteLine("Test Stage 3");
                var elapsed = TestPhase(3, path, verify);
                Console.WriteLine("Test Stage 3 completed in {0}", elapsed);
                elapsed3 += elapsed;
            }
            elapsed3 /= MAX_PHASE3;

            Stats.DisplayContextTime();
            Console.WriteLine("====");
            var totalTime = elapsed1 + elapsed2 + elapsed3;
            Console.WriteLine("Total time elapsed: {0}", totalTime);
            Log.Info("Memory footprint: {0}", GC.GetTotalMemory(false));
            return totalTime;
        }

        Stats byMask = new Stats();
        Stats byParam = new Stats();
        static byte[] EmptyBody = Encoding.ASCII.GetBytes("");

        public TimeSpan TestPhase(int phase, string path, bool verify)
        {
            Log.Info("Testing phase " + phase);
            TimeSpan totalPhaseTime = new TimeSpan();
            var getPost = phase == 2 ? "post" : "get";
            var ammoFileName = path + "/ammo/phase_" + phase + "_" + getPost + ".ammo";
            var answersFileName = path + "/answers/phase_" + phase + "_" + getPost + ".answ";
            var postActions = new List<Action>();
            var ctx = new HttpCtx();

            using (var reqFs = new FileStream(ammoFileName, FileMode.Open, FileAccess.Read))
            using (var reqStream = new StreamReader(reqFs, Encoding.UTF8))
            using (var ansFs = new FileStream(answersFileName, FileMode.Open, FileAccess.Read))
            using (var ansStream = new StreamReader(ansFs, Encoding.ASCII, false))
            {
                while (!reqStream.EndOfStream)
                {
                    prepareHttpContext(reqStream, ctx);
                    var startTime = Stats.Watch.Elapsed;
                    router.ProcessRequest(ctx); // actual request processing
                    if (ctx.PostAction != null)
                    {
                        postActions.Add(ctx.PostAction);
                        ctx.PostAction = null;
                    }
                    var elapsed = Stats.Watch.Elapsed-startTime;
                    Stats.ReportContextTime(ctx.ContextType, elapsed);
                    totalPhaseTime += elapsed;

                    if (verify)
                    {
                        var ans = ansStream.ReadLine().Split('\t');
                        var expStatus = int.Parse(ans[2]);
                        var strAnsw = ""; // Encoding.ASCII.GetString(ctx.Buffer, ctx.ResponseStart, ctx.ResponseBodyStart- ctx.ResponseStart);
                        if (ans.Length > 3)
                            strAnsw = ans[3];

                        //var qPath = ctx.Request.Path.ToString();
                        if (ctx.StatusCode != expStatus)
                        {
                            Console.WriteLine("\n{0} {1}", ans[0], Uri.UnescapeDataString(ans[1]));
                            Console.WriteLine("Received statusCode {0} instead of {1}",
                                ctx.StatusCode, expStatus);
                            router.ProcessRequest(ctx);
                        }
                        else
                        if (strAnsw.Length == 0 && ctx.ResponseBodyLength == 0)
                        {
                            // both null, everything is OK
                        }
                        else
                        {
                            // compare jsons
                            object jsResp = null;
                            object jsAnsw = null;
                            try
                            {
                                jsResp = JsonSerializer.Deserialize<dynamic>(ctx.Buffer, ctx.ResponseBodyStart);
                                jsAnsw = JsonSerializer.Deserialize<dynamic>(strAnsw);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                                break;
                            }
                            if (!compareDynamic(jsAnsw, jsResp))
                            {
                                //compareDynamic(jsAnsw, jsResp);
                                Console.WriteLine("\n{0} {1}", ans[0], Uri.UnescapeDataString(ans[1]));
                                Console.WriteLine("\tReceived\n{0}\n\tInstead of\n{1}",
                                    Encoding.UTF8.GetString(ctx.Buffer, ctx.ResponseBodyStart, ctx.ResponseBodyLength),
                                    Encoding.UTF8.GetString(JsonSerializer.Serialize<dynamic>(jsAnsw))
                                );
                                //Console.ReadLine();
                                router.ProcessRequest(ctx);
                            }
                        }
                    }
                    ctx.Reset();
                }
            }

            // process post-actions
            if( verify ) // if not verifying, likely testing for performance
                foreach (var a in postActions)
                    a.Invoke();
            postActions.Clear();

            return totalPhaseTime;
        }


        private bool compareDynamic(dynamic obj1, dynamic obj2)
        {
            // compare simple types
            if (obj1 == null && obj2 == null)
                return true;
            if (obj1 == null || obj2 == null)
            {
                Console.WriteLine("One of the objects is null");
                return false;
            }
            if (obj1.GetType() != obj2.GetType())
            {
                Console.WriteLine("Incompatible types");
                return false;
            }

            // compare objects
            if (obj1 is Dictionary<string, object>)
            {
                var d1 = obj1 as Dictionary<string, object>;
                var d2 = obj2 as Dictionary<string, object>;
                if (d1.Count != d2.Count)
                {
                    Console.WriteLine("Different number of properties");
                    return false;
                }
                foreach (var key in d1.Keys)
                {
                    if (!d2.ContainsKey(key))
                    {
                        Console.WriteLine("d2 does not contain " + key);
                        return false;
                    }
                    if (!compareDynamic(d1[key], d2[key]))
                    {
                        Console.WriteLine("! Got {0}={1} instead of {2}", key, d2[key], d1[key]);
                        if (d1.ContainsKey("id"))
                            Console.WriteLine("id=" + d1["id"]);
                        return false;
                    }
                }
                return true;
            }

            // compare lists
            if (obj1 is List<dynamic>)
            {
                var l1 = obj1 as List<dynamic>;
                var l2 = obj2 as List<dynamic>;
                if (l1.Count != l2.Count)
                {
                    Console.WriteLine("Different array counts");
                    return false;
                }
                //l1.Sort();
                //l2.Sort();
                for (int i = 0; i < l1.Count; i++)
                    if (!compareDynamic(l1[i], l2[i]))
                        return false;
                return true;
            }

            return obj1 == obj2;
        }

        private void prepareHttpContext(StreamReader reader, HttpCtx ctx)
        {
            //var size = int.Parse(reader.ReadLine().Split(' ')[0]);
            ctx.Reset();
            var line = reader.ReadLine(); // skip the 1st line
            do
            {
                line = reader.ReadLine();
                if (ctx.Method==null)
                    ctx.ParseFirstLine(line);
                else
                if (ctx.RequestBodyLength == 0)
                    line.FindIntegerAfter("content-length", out ctx.RequestBodyLength);
            } while (!string.IsNullOrEmpty(line));

            if (ctx.RequestBodyLength > 0)
            {
                Span<char> spanChar = stackalloc char[ctx.RequestBodyLength];
                Span<byte> spanByte = ctx.Buffer.AsSpan()
                    .Slice(ctx.RequestBodyStart, ctx.RequestBodyLength);
                reader.Read(spanChar);
                for (int i = 0; i < ctx.RequestBodyLength; i++)
                    spanByte[i] = (byte)spanChar[i];
                reader.ReadLine(); // empty line
            }
        }

    }
}
