using System;
using System.Diagnostics;
using System.Net;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hl18
{
    public interface ICtxProcessor
    {
        // return the status (200, 201, 202, 400, 404), update ctx.ResponseBodyLength if used
        // special 200 codes: 211 (empty accounts), 212 (empty groups)
        int Process(HttpCtx ctx, int id);
    }

    public enum EPhase
    {
        P01, // before the start
        P1,  // phase 1 is in progress
        P12, // pause after phase 1
        P2,  // phase 2 is in progress
        P23, // pause after phase 2
        P3,  // phase 3 is in progress
        PEND // phase 3 completed
    };

    // the main router
    public class Router
    {
        private readonly Storage store;
        public EPhase Phase { get; private set; }

        // public for warming up
        public readonly ICtxProcessor getFilter;
        public readonly ICtxProcessor getGroup;
        public readonly ICtxProcessor getRecommend;
        public readonly ICtxProcessor getSuggest;
        public readonly ICtxProcessor postNew;
        public readonly ICtxProcessor postUpdate;
        public readonly ICtxProcessor postLikes;
        public readonly Warmup warmer;
        private int warmId;

        public Router(Storage storage)
        {
            store = storage;
            // controllers
            getFilter = new GetFilter(storage);
            getGroup = new GetGroup(storage);
            getRecommend = new GetRecommend(storage);
            getSuggest = new GetSuggest(storage);
            postNew = new PostNew(storage);
            postUpdate = new PostUpdate(storage);
            postLikes = new PostLikes(storage);
            warmer = new Warmup(this, store);

            // number of requests by phase
            switch(store.IsRatingRun)
            {
                case -1: // local set
                    phase1Gets = 2_400;
                    phase2Posts = 10_000;
                    phase3Gets = 10_000;
                    break;
                case 0: // test run
                    phase1Gets = 3_000;
                    phase2Posts = 10_000;
                    phase3Gets = 15_000;
                    break;
                case 1: // rating run
                    phase1Gets = 27_000;
                    phase2Posts = 90_000;
                    phase3Gets = 60_000;
                    break;
            }
        }

        public Storage GetStorage() => store;

        // header parts
        public static byte[] Status200 = Encoding.ASCII.GetBytes("200 OK         ");
        public static byte[] Status201 = Encoding.ASCII.GetBytes("201 Created    ");
        public static byte[] Status202 = Encoding.ASCII.GetBytes("202 Accepted   ");
        public static byte[] Status400 = Encoding.ASCII.GetBytes("400 Bad Request");
        public static byte[] Status404 = Encoding.ASCII.GetBytes("404 Not Found  ");

        // shared response headers
        private static byte[] responseHeaders = Encoding.ASCII.GetBytes(new StringBuilder(256)
            .Append("HTTP/1.1 400 Bad Request\r\n")
            .Append("Content-Length: 0    \r\n")
            .Append("Connection: keep-alive\r\n")
            .Append("Content-Type: application/json; charset=utf-8\r\n")
            .Append("\r\n").ToString());
        private static int STATUS_POS = 9;
        private static int LENGTH_POS = 42;

        // typical bodies and their encoded lengths
        private static byte[] emptyJson = Encoding.ASCII.GetBytes("{}"); // code 210
        private static byte[] emptyAccounts = Encoding.ASCII.GetBytes("{\"accounts\":[]}"); // code 211
        private static byte[] emptyGroups = Encoding.ASCII.GetBytes("{\"groups\":[]}"); // code 212
        static byte[] zeroLength = new byte[] { 48, 32, 32, 32, 32 };
        static byte[] emptyJsonLength = new byte[] { 50, 32, 32, 32, 32 };
        static byte[] emptyAccountsLength = new byte[] { 49, 53, 32, 32, 32 };
        static byte[] emptyGroupsLength = new byte[] { 49, 51, 32, 32, 32 };

        // main routing code (must be already serialized and ordered for POSTs!)
        public int ProcessRequest(HttpCtx ctx)
        {
            lastCall = Stats.Watch.Elapsed;

            // fill the header
            Array.Copy(responseHeaders, 0, ctx.Buffer, ctx.ResponseStart, responseHeaders.Length);
            ctx.ResponseBodyStart = ctx.ResponseStart + responseHeaders.Length;

            // parse the 1st header, parts[0]=method, parts[1]=path+params, path[2]="HTTP/1.1"
            if (ctx.Path == null)
            {
                Log.Error("Path: {0}", ctx.Path);
                return sendReply(ctx, 400);
            }
            var path = ctx.Path.Split('/');
            if (path.Length < 4 || path[1] != "accounts")
                return sendReply(ctx, 404);

            if ( ctx.Method == "GET" )
            {
                Interlocked.Increment(ref getCounter);
                // /accounts/filter/
                if (path[2] == "filter")
                    if (!path[3].IsEmpty)
                        return sendReply(ctx, 404);
                    else
                        return sendReply( ctx, getFilter.Process(ctx, 0) );

                // /accounts/group/
                if (path[2] == "group")
                    if (!path[3].IsEmpty)
                        return sendReply(ctx, 404);
                    else
                        return sendReply(ctx, getGroup.Process(ctx, 0));

                // /accounts/{id}/...
                if (path.Length < 5 )
                    return sendReply(ctx, 404);
                if (!path[2].TryToInt(out var id))
                    return sendReply(ctx, 404);

                // accounts/{id}/recommend/
                if (path[3] == "recommend")
                    return sendReply(ctx, getRecommend.Process(ctx, id));

                // accounts/{id}/suggest/
                if (path[3] == "suggest")
                    return sendReply(ctx, getSuggest.Process(ctx, id));

                // no other GETs supported
                return sendReply(ctx, 404);
            }
            if ( ctx.Method == "POST" )
            {
                Interlocked.Increment(ref postCounter);
                // /accounts/new/
                if (path[2] == "new")
                    if (!path[3].IsEmpty)
                        return sendReply(ctx, 404);
                    else
                        return sendReply(ctx, postNew.Process(ctx, 0));

                // /accounts/likes/
                if (path[2] == "likes")
                    if (!path[3].IsEmpty)
                        return sendReply(ctx, 404);
                    else
                        return sendReply(ctx, postLikes.Process(ctx, 0));

                // /accounts/{id}/
                if (path[2].TryToInt(out var id))
                    if (!path[3].IsEmpty)
                        return sendReply(ctx, 404);
                    else
                        return sendReply(ctx, postUpdate.Process(ctx, id));

                // no other POSTs supported
                return sendReply(ctx, 404);
            }

            // all other methods are not supported
            return sendReply(ctx, 404);
        }

        /******************************** Helpers **********************************/

        // sync! returns the reply length
        private int sendReply(HttpCtx ctx, int statusCode)
        {
            ctx.StatusCode = statusCode;
            switch (statusCode)
            {
                case 200: // 200 OK with optional body
                    Array.Copy( Status200, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status200.Length);
                    AStringBuilder.ComposeInt(ctx.ResponseBodyLength, ctx.Buffer, ctx.ResponseStart + LENGTH_POS);
                    break;

                case 201:
                case 202: // empty jsons
                    if ( statusCode==201)
                        Array.Copy(Status201, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status201.Length);
                    else
                        Array.Copy(Status202, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status202.Length);
                    Array.Copy(emptyJsonLength, 0, ctx.Buffer, ctx.ResponseStart + LENGTH_POS, emptyJsonLength.Length);
                    Array.Copy(emptyJson, 0, ctx.Buffer, ctx.ResponseBodyStart, emptyJson.Length);
                    ctx.ResponseBodyLength = 2;
                    break;

                case 211: // empty accounts
                    ctx.StatusCode = 200;
                    Array.Copy(Status200, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status200.Length);
                    Array.Copy(emptyAccountsLength, 0, ctx.Buffer, ctx.ResponseStart + LENGTH_POS, emptyAccountsLength.Length);
                    Array.Copy(emptyAccounts, 0, ctx.Buffer, ctx.ResponseBodyStart, emptyAccounts.Length);
                    ctx.ResponseBodyLength = emptyAccounts.Length;
                    break;

                case 212: // empty groups
                    ctx.StatusCode = 200;
                    Array.Copy(Status200, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status200.Length);
                    Array.Copy(emptyGroupsLength, 0, ctx.Buffer, ctx.ResponseStart + LENGTH_POS, emptyGroupsLength.Length);
                    Array.Copy(emptyGroups, 0, ctx.Buffer, ctx.ResponseBodyStart, emptyGroups.Length);
                    ctx.ResponseBodyLength = emptyGroups.Length;
                    break;

                case 400: // 400 bad request, empty body
                    Array.Copy(Status400, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status400.Length);
                    Array.Copy(zeroLength, 0, ctx.Buffer, ctx.ResponseStart + LENGTH_POS, zeroLength.Length);
                    ctx.ResponseBodyLength = 0;
                    break;

                case 404: // 404 not found, empty body
                    Array.Copy(Status404, 0, ctx.Buffer, ctx.ResponseStart + STATUS_POS, Status404.Length);
                    Array.Copy(zeroLength, 0, ctx.Buffer, ctx.ResponseStart + LENGTH_POS, zeroLength.Length);
                    ctx.ResponseBodyLength = 0;
                    break;
            }
            var responseLength = responseHeaders.Length + ctx.ResponseBodyLength;
            //ctx.Complete(responseLength);
            return responseLength;
        }

        public void ProcessWarmup()
        {
            var ctx = HttpCtx.Obtain();
            warmer.GetOnce(ctx, warmId++);
            HttpCtx.Release(ctx);
        }

        public delegate void PhaseChanged(EPhase phase);
        public event PhaseChanged OnPhaseChanged;

        public void Tick()
        {
            int useTimeout = 0; // using timeout is dangerous with cached requests

            switch (Phase)
            {
                case EPhase.P01:
                    if (getCounter > 0)
                    {
                        Phase = EPhase.P1;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 1 has started");
                        lastCall = Stats.Watch.Elapsed;
                    }
                    break;
                case EPhase.P1:
                    if (getCounter >= phase1Gets || useTimeout>0 && (Stats.Watch.Elapsed - lastCall) > TimeSpan.FromMilliseconds(useTimeout) )
                    {
                        Phase = EPhase.P12;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 1 has ended");
                        GetGroup.CachedResults.Clear();
                        GetFilter.CachedResults.Clear();
                        Garbage.CollectAll();
                    }
                    if(postCounter > 0/*how did we miss it?*/)
                    {
                        GetGroup.CachedResults.Clear();
                        GetFilter.CachedResults.Clear();
                        Phase = EPhase.P2;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 2 has started unexpectedly");
                    }
                    break;
                case EPhase.P12:
                    if (postCounter > 0)
                    {
                        Phase = EPhase.P2;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 2 has started");
                        lastCall = Stats.Watch.Elapsed;
                    }
                    break;
                case EPhase.P2:
                    if (postCounter >= phase2Posts || useTimeout>0 && (Stats.Watch.Elapsed - lastCall) > TimeSpan.FromMilliseconds(useTimeout))
                    {
                        Phase = EPhase.P23;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 2 has ended");
                        GetGroup.CachedResults.Clear();
                        GetFilter.CachedResults.Clear();
                        Garbage.CollectAll();
                        store.Prepare(false);
                        Garbage.CollectAll();
                    }
                    break;
                case EPhase.P23:
                    if (getCounter > phase1Gets)
                    {
                        Phase = EPhase.P3;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 3 has started");
                        lastCall = Stats.Watch.Elapsed;
                    }
                    break;
                case EPhase.P3:
                    if (getCounter >= phase1Gets + phase3Gets || useTimeout>0 && (Stats.Watch.Elapsed - lastCall) > TimeSpan.FromMilliseconds(useTimeout))
                    {
                        Phase = EPhase.PEND;
                        OnPhaseChanged(Phase);
                        Log.Info("Phase 3 has ended");
                        Stats.DisplayContextTime();
                        Mapper.DisplayStats();
                        EpollServer.DisplayServerStats();
                    }
                    break;
            }

            // 5 sec report
            var now = Stats.Watch.Elapsed;
            if ( (now-lastReport).TotalMilliseconds > 4000 )
            {
                lastReport = now;
                if (Phase != EPhase.P01)
                    Console.WriteLine("{0} M{1} S{2}/{3}", lastReport.ToString(@"mm\:ss"),
                        GC.GetTotalMemory(false) / 1024 / 1024, EpollHandler.OpenedSockets, EpollHandler.ClosedSockets);
            }

        }
        private int phase1Gets = 0;
        private int phase2Posts = 0;
        private int phase3Gets = 0;
        private TimeSpan lastReport;
        private volatile int getCounter = 0;
        private volatile int postCounter = 0;
        private TimeSpan lastCall;

    }
}
