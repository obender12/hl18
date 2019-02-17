using Mono.Unix.Native;
using System;
using System.Runtime.InteropServices;

namespace hl18
{
    // all functions of this class are called from a single I/O thread
    public class EpollHandler : HttpCtx
    {
        private readonly EpollServer server;
        private readonly GCHandle bufferHandle;
        private readonly IntPtr bufferPtr;
        private TimeSpan processingStarted;


        // handler state
        enum EState
        {
            Idle,
            Reading,
            Processing,
            Writing,
        }
        private EState state = EState.Idle;
        private int totalRead = 0;
        private int totalWritten = 0;
        private int socketHandle = -1;
        public int GetSocket() => socketHandle;


        private void setIdle()
        {
            if (state != EState.Idle)
            {
                totalRead = 0;
                totalWritten = 0;
                socketHandle = -1;
                Reset();
                state = EState.Idle;
                Log.Epoll("setState="+state);
            }
            else
                Log.Warning("Already in Idle");
        }

        private void setReading()
        {
            if (state != EState.Reading)
            {
                totalRead = 0;
                Reset();
                state = EState.Reading;
                Log.Epoll("setState=" + state);
                //OnReadReady();
            }
        }

        private void setProcessing()
        {
            if (state != EState.Processing)
            {
                ResponseBodyStart = 0;
                ResponseBodyLength = 0;
                state = EState.Processing;
                Log.Epoll("setState=" + state);
            }
            else
                Log.Warning("Already in Processing");
        }

        private void setWriting()
        {
            if (state != EState.Writing)
            {
                totalWritten = 0;
                state = EState.Writing;
                Log.Epoll("setState=" + state);
                OnWriteReady();
            }
        }


        // constructor
        public EpollHandler(EpollServer server)
            : base() // base class is a reusable HttpCtx
        {
            this.server = server;
            bufferHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            bufferPtr = bufferHandle.AddrOfPinnedObject();

            state = EState.Idle;
            totalRead = 0;
            totalWritten = 0;
        }


        public void OnAttach(int socket)
        {
            if (state != EState.Idle)
                Log.Error("Attaching socket in state " + state);
            //Console.Write('+');
            socketHandle = socket;
            OpenedSockets++;
            processingStarted = Stats.Watch.Elapsed;
            Log.Epoll("[{0}] Socket attached", socketHandle);
            
            // disable Nagle algorithm
            if (Syscall.setsockopt(socketHandle, UnixSocketProtocol.IPPROTO_TCP, (UnixSocketOptionName)1, 1) < 0)
                Log.Error($"Failed to setsockopt(TCP_NODELAY): {Stdlib.GetLastError()}");

            // enable QuickAck
            //if (Syscall.setsockopt(socketHandle, UnixSocketProtocol.IPPROTO_TCP, (UnixSocketOptionName)12, 1) < 0)
            //    Log.Error($"Failed to setsockopt(SO_QUICKACK): {Stdlib.GetLastError()}");

            // worth giving it a shot (no errors checked)
            Syscall.setsockopt(socketHandle, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_BUSY_POLL, 1_000_000);
            Syscall.setsockopt(socketHandle, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_SNDBUF, 6000);
            Syscall.setsockopt(socketHandle, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_RCVBUF, 6000);

            setReading();
        }

        public static int OpenedSockets = 0;
        public static int ClosedSockets = 0;

        public int OnDetach()
        {
            if (state != EState.Reading || totalRead>0 )
                Log.Error("Detaching socket from state "+state);

            Log.Epoll("[{0}] Socket detached", socketHandle);

            processingStarted = new TimeSpan();
            var s = socketHandle;
            if (s > 0)
            {
                //Console.Write('-');
                ClosedSockets++;
            }
            setIdle(); // will clear reset the socket to -1
            return s;
        }

        // called either from OnAttach or on EPOLLIN
        public int OnReadReady()
        {
            Log.Epoll("[{0}] OnReadReady()", socketHandle);
            if (state != EState.Reading)
            {
                Log.Error("OnReadReady() in state " + state);
            }
            if (socketHandle < 0)
            {
                Log.Error("Error: OnReadReady on detached socket");
                return 0;
            }

            var read = Syscall.read(socketHandle,
                IntPtr.Add(bufferPtr, totalRead),
                (ulong)(Buffer.Length - totalRead));
            Log.Epoll("[{0}] read() returned {1}", socketHandle, read);

            if (read < 0)
            {
                var errno = Stdlib.GetLastError();
                if (errno == Errno.EAGAIN || errno == Errno.EWOULDBLOCK || errno == 0)
                {
                    return 0; // continue in the Reading state
                }
                Log.Error("[{0}] read() socket error {1}", socketHandle, errno);
                return -1; // waiting for the socket to be detached/closed
            }
            if (read == 0)
            {
                Log.Epoll("[{0}] peer closed connection", socketHandle);
                return -1;
            }

            if (totalRead == 0 && processingStarted.Ticks==0 )
                processingStarted = Stats.Watch.Elapsed;

            totalRead += (int)read;
            if (RequestLength == 0)
                ParseRequestHeader(totalRead);
            if (RequestLength == 0 || RequestLength > 0 && totalRead < RequestLength)
            {
                return (int)read; // continue in the Reading state
            }

            // done!
            //Console.Write('<');
            Log.Epoll("[{0}] Read completed, size={1}", socketHandle, RequestLength);
            if (RequestLength > MaxRequestSize)
                MaxRequestSize = RequestLength;

            // process the request
            setProcessing();
            Log.Epoll("[{0}] Send to processing, query_id={1}", socketHandle, QueryId);

            var os = new Overspan(20);
            server.ProcessContext(this);
            os.Check(ContextType);

            return 0;
        }

        public static int MaxRequestSize = 0;
        public static int MaxResponseSize = 0;

        // called by the server after completing the request processing
        public void OnDoneProcessing()
        {
            if (state != EState.Processing)
                Log.Error("DoneProcessing() in state " + state);

            if (ResponseTotalLength == 0)
            {
                Log.Error("Context query_id={0} has zero response",
                    QueryId, socketHandle);
                setReading();
                return;
            }
            if (ResponseTotalLength > MaxRequestSize)
                MaxResponseSize = ResponseTotalLength;

            // don't register with epoll, just start writing
            setWriting();
        }


        // can be called in the context of a worker thread
        public int OnWriteReady()
        {
            if (state != EState.Writing)
            {
                //Log.Error("Writing in state " + state);
                return 0;
            }
            Log.Epoll("[{0}] Writing {1} bytes from {2}", socketHandle,
                ResponseTotalLength - totalWritten, ResponseStart + totalWritten);
            var written = Syscall.write(socketHandle,
                IntPtr.Add(bufferPtr, ResponseStart + totalWritten),
                (ulong)(ResponseTotalLength - totalWritten));
            Log.Epoll("[{0}] write() returned {1}", socketHandle, written);

            if (written < 0)
            {
                var errno = Stdlib.GetLastError();
                if (errno != 0 && errno != Errno.EAGAIN)
                {
                    Log.Error("[{0}] write() socket error {1}", socketHandle, errno);
                    return -1; // waiting for the socket to be detached/closed
                }
                return 0;
            }
            if (written == 0)
            {
                Log.Epoll("[{0}] peer closed connection", socketHandle);
                // waiting for the socket to be detached/closed
                return 0;
            }
            Log.Epoll("[{0}] Written {1} bytes", socketHandle, written);
            totalWritten += (int)written;
            if (totalWritten < ResponseTotalLength)
            {
                // not all was written, try again
            }
            else
            {
                //Console.Write('>');
                Log.Epoll("[{0}] Request processing complete!", socketHandle);
                Stats.ReportContextTime(ContextType, Stats.Watch.Elapsed - processingStarted);
                processingStarted = new TimeSpan();
                setReading();
            }
            return 0;
        }

        public bool IsPost()
        {
            return !Method.IsEmpty && Method[0] == (byte)'P';
        }

        public EpollEvents GetEvents()
        {
            var events = 
                EpollEvents.EPOLLIN
              | EpollEvents.EPOLLET
              | EpollEvents.EPOLLONESHOT
              | EpollEvents.EPOLLRDHUP
            //| (EpollEvents)(1 << 28) // EPOLLEXCLUSIVE
            | EpollEvents.EPOLLHUP
            ;
            if (state == EState.Writing)
                events |= EpollEvents.EPOLLOUT;
            return events;
        }



    }
}
