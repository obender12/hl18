using Mono.Unix.Native;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace hl18
{
    // coordinating server
    public class EpollServer
    {
        private readonly Router router;

        public readonly EpollListener listener;

        private EpollHandler[] handlers = new EpollHandler[8192];
        public EpollHandler GetHandler(int socket)
        {
            if (handlers[socket] == null)
                handlers[socket] = new EpollHandler(this);
            return handlers[socket];
        }

        //private ConcurrentBag<EpollHandler> getQueue = new ConcurrentBag<EpollHandler>(); // order not important
        private ConcurrentQueue<Action> postQueue = new ConcurrentQueue<Action>(); // order is important, speed is not

        // constructor
        public EpollServer(Router router)
        {
            this.router = router;
#if !EPOLL_DEBUG
            // pre-create 2000 handlers
            for (int i = 0; i < 2048; i++)
                handlers[i] = new EpollHandler(this);
#endif
            router.OnPhaseChanged += (phase) =>
            {
                if (phase == EPhase.P1 || phase == EPhase.P2 || phase == EPhase.P3)
                    timeoutMode = 0;
                else
                    timeoutMode = -1;
            };

            // listener single instance 
            listener = new EpollListener(this);
        }


        public static void DisplayServerStats()
        {
            Log.Info("MaxRequest: " + EpollHandler.MaxRequestSize);
            Log.Info("MaxResponse: " + EpollHandler.MaxResponseSize);
            Log.Info("HandlersCreated: " + EpollListener.HandlersCreated);
        }

        // initiate the main loop
        public void Run()
        {
#if !EPOLL_DEBUG
            new Thread(ioLoop).Start();
            new Thread(ioLoop).Start();
            new Thread(ioLoop).Start();
#endif
            // set the CPU affinity
            Process process = Process.GetCurrentProcess();
            Console.WriteLine("Threads: "+process.Threads.Count);
            for (int i = 0; i < process.Threads.Count; ++i)
            {
                //process.Threads[i].ProcessorAffinity = (IntPtr)(1L << 1);
                process.Threads[i].IdealProcessor = i%4;
            }

#if !EPOLL_DEBUG
            // shoot with a single GET request
            Log.Info("Warming up the server");
            using (var client = new HttpClient())
                for( int i=0; i<10000; i++)
                    client.GetAsync("http://localhost/").Wait();
#endif

            // main thread is the main loop
            Log.Info("Ready");
            mainLoop(null);
        }

        private int timeoutMode = -1;

        // busy wait
        private void ioLoop(object threadObj)
        {
            try
            {
                int MAX_EVENTS = 256;
                EpollEvent[] events = new EpollEvent[MAX_EVENTS];

                while (true)
                    listener.PollOnce(events, timeoutMode);
            }
            catch( Exception  e)
            {
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
            }
        }

        // blocked on postQueue
        private void mainLoop(object threadObj)
        {
            try
            {
                int MAX_EVENTS = 256;
                EpollEvent[] events = new EpollEvent[MAX_EVENTS];

                while (true)
                {
                    listener.PollOnce(events, timeoutMode*(-100) );
                    while (postQueue.TryDequeue(out var action))
                        action.Invoke();
                    router.Tick();
                }
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
            }
        }

        private byte[] dummy = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nServer: HL18\r\nContent-Type: application/json\r\nContent-Length: 0\r\n\r\n");

        // process fully received request
        public void ProcessContext(EpollHandler ctx)
        {
#if true
            router.ProcessRequest(ctx);
            if (ctx.PostAction != null)
                postQueue.Enqueue(ctx.PostAction);
            ctx.PostAction = null;
            ctx.OnDoneProcessing();
#else
            Array.Copy(dummy, 0, ctx.Buffer, ctx.ResponseStart, dummy.Length);
            ctx.ResponseBodyStart = ctx.ResponseStart + dummy.Length;
            ctx.ResponseBodyLength = dummy.Length;
            ctx.OnDoneProcessing();
#endif
        }

    }
}
