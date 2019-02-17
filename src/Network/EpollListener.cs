using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Mono.Unix.Native;

namespace hl18
{
    public class EpollListener
    {
        private readonly int listenerSocket;
        private readonly int epfd;
        private readonly EpollServer server;

        // constructor
        public EpollListener(EpollServer server)
        {
            this.server = server; // for creating handlers

            // create the epoll fd
            epfd = Syscall.epoll_create(1);
            if (epfd < 0)
                throw new Exception($"Call to {nameof(Syscall.epoll_create)} failed with code: {Stdlib.GetLastError()}");

            // create and attach the listener socket
            listenerSocket = Syscall.socket(UnixAddressFamily.AF_INET, UnixSocketType.SOCK_STREAM, UnixSocketProtocol.IPPROTO_TCP);
            Log.Epoll("Listener socket is {0}", listenerSocket);
            OnAttach(listenerSocket);

            // bind to port 80
            var endpoint = new IPEndPoint(IPAddress.Any, 80);
            bind(endpoint, 1024);
            Log.Info("Listening on port " + endpoint.Port);
        }

        public int GetSocket() => listenerSocket;

        public void OnAttach(int socket)
        {
            // Allow address and port reuse
            if (Syscall.setsockopt(listenerSocket, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_REUSEADDR, 1) < 0)
                Log.Error("Failed to set SO_REUSEADDR");
            //Syscall.setsockopt(listenerSocket, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_REUSEPORT, 1);

            // enable QuickAck
            //if (Syscall.setsockopt(listenerSocket, UnixSocketProtocol.IPPROTO_TCP, (UnixSocketOptionName)12, 1) < 0)
            //    Log.Error($"Failed to setsockopt(): {Stdlib.GetLastError()}");

            // worth giving it a shot
            Syscall.setsockopt(listenerSocket, UnixSocketProtocol.SOL_SOCKET, UnixSocketOptionName.SO_BUSY_POLL, 1_000_000);

            var opts = Syscall.fcntl(listenerSocket, FcntlCommand.F_GETFL);
            if (opts < 0)
                throw new IOException($"Failed to get openflags from handle: {Stdlib.GetLastError()}");
            opts |= (int)OpenFlags.O_NONBLOCK;
            if (Syscall.fcntl(listenerSocket, FcntlCommand.F_SETFL, opts) < 0)
                throw new IOException($"Failed to set socket O_NOBLOCK: {Stdlib.GetLastError()}");

            opts = Syscall.fcntl(listenerSocket, FcntlCommand.F_GETFD);
            if (opts < 0)
                throw new IOException($"Failed to get openflags from handle: {Stdlib.GetLastError()}");
            opts |= 1;
            if (Syscall.fcntl(listenerSocket, FcntlCommand.F_SETFD, opts) < 0)
                throw new IOException($"Failed to set socket FC_CLOEXEC: {Stdlib.GetLastError()}");

            // listen to listener socket events
            Add(listenerSocket, GetEvents());
        }

        public int OnDetach()
        {
            Log.Error("Listener socket unexpectedly detached");
            return listenerSocket;
        }

        private void bind(IPEndPoint endpoint, int backlog)
        {
            Sockaddr servaddr = new SockaddrIn()
            {
                sa_family = UnixAddressFamily.AF_INET,
                sin_family = UnixAddressFamily.AF_INET,
                sin_addr = new InAddr() { s_addr = BitConverter.ToUInt32(endpoint.Address.GetAddressBytes(), 0) },
                sin_port = Syscall.htons((ushort)endpoint.Port)
            };

            // Bind so we are attached
            var ret = Syscall.bind(listenerSocket, servaddr);
            if (ret < 0)
                throw new IOException($"Failed to bind to endpoint: {Stdlib.GetLastError()}");

            // start listening
            ret = Syscall.listen(listenerSocket, backlog);
            if (ret < 0)
                throw new IOException($"Failed to set socket to listen: {Stdlib.GetLastError()}");
        }

        public bool Add(int fd, EpollEvents flags)
        {
            var ev = new EpollEvent()
            {
                events = flags,
                u32 = (uint)fd
            };

            Log.Epoll("[{0}] EPOLL_CTL_ADD({1})", fd, flags);
            var r = Syscall.epoll_ctl(epfd, EpollOp.EPOLL_CTL_ADD, fd, ref ev);
            if (r != 0)
            {
                Log.Error($"Call to epoll_ctl(ADD) failed with code {r}: {Stdlib.GetLastError()}");
                return false;
            }
            return true;
        }

        public bool Remove(int fd, EpollEvents flags)
        {
            var ev = new EpollEvent()
            {
                events = flags,
                u32 = (uint)fd
            };

            Log.Epoll("[{0}] EPOLL_CTL_DEL({1})", fd, flags);
            var r = Syscall.epoll_ctl(epfd, EpollOp.EPOLL_CTL_DEL, fd, ref ev);
            if (r != 0)
            {
                Log.Error($"Call to epoll_ctl(DEL) failed with code {r}: {Stdlib.GetLastError()}");
                return false;
            }
            return true;
        }

        public bool Update(int fd, EpollEvents flags)
        {
            var ev = new EpollEvent()
            {
                events = flags,
                u32 = (uint)fd
            };

            Log.Epoll("[{0}] EPOLL_CTL_MOD({1})", fd, flags);
            var r = Syscall.epoll_ctl(epfd, EpollOp.EPOLL_CTL_MOD, fd, ref ev);
            if (r != 0)
            {
                Log.Error($"Call to epoll_ctl(MOD) failed with code {r}: {Stdlib.GetLastError()}");
                return false;
            }
            return true;
        }


        public void PollOnce(EpollEvent[] events, int timeoutMode = -1)
        {
            if( timeoutMode<0 )
                Log.Epoll("Listener waiting for signals");

            var count = Syscall.epoll_wait(epfd, events, events.Length, timeoutMode);

            if (count < 0)
            {
                Log.Error($"Call to epoll_wait() failed with code {count}: {Stdlib.GetLastError()}");
                return;
            }

            if (count > 0)
                Log.Epoll("+"+count);
            for (var i = 0; i < count; i++)
            {
                var ev = events[i];
                Log.Epoll("Epoll[{0}] got {1} for {2}", i, ev.events, ev.u32);

                // find the handler
                var socket = (int)ev.u32;
                var handler = server.GetHandler(socket);

                if ( ((int)ev.events & disconnectMask) > 0)
                {
                    disconnect(handler);
                }
                else
                if (ev.events.HasFlag(EpollEvents.EPOLLIN))
                {
                    if (socket == listenerSocket)
                    {
                        OnReadReady();
                        Update(socket, GetEvents());
                    }
                    else
                    {
                        var os = new Overspan(50);
                        var ret = handler.OnReadReady();
                        os.Check("OnReadReady()");
                        if (ret < 0)
                            disconnect(handler);
                        else
                            Update(socket, handler.GetEvents());
                    }
                }
                else
                if (ev.events.HasFlag(EpollEvents.EPOLLOUT))
                {
                    var os = new Overspan(50);
                    var ret = handler.OnWriteReady();
                    os.Check("OnWriteReady()");
                    if (ret < 0)
                        disconnect(handler);
                    else
                        Update(socket, handler.GetEvents());
                }
            }
        }

        public EpollEvents GetEvents() => EpollEvents.EPOLLIN | EpollEvents.EPOLLET | EpollEvents.EPOLLONESHOT;

        private static int disconnectMask =
            (int)EpollEvents.EPOLLRDHUP |
            (int)EpollEvents.EPOLLHUP |
            (int)EpollEvents.EPOLLERR;


        public int OnReadReady()
        {
            int ret = 0;
            while (ret >= 0)
            {
                // Try to read; we a non-blocking
                var addr = new Sockaddr();
                ret = Syscall.accept4(listenerSocket, addr, UnixSocketFlags.SOCK_NONBLOCK);

                // If we did not get a socket
                if (ret < 0)
                {
                    var errno = Stdlib.GetLastError();
                    if (errno == Errno.EAGAIN || errno == Errno.EWOULDBLOCK)
                        return 1;
                    Log.Error($"Failed to accept socket: {errno}");
                    return -1;
                }

                // got a new socket, create a new connection
                if (ret >= 0)
                {
                    var socketHandle = ret;
                    Log.Epoll("Accepted socket {0}", socketHandle);
                    var handler = server.GetHandler(ret);
                    handler.OnAttach(socketHandle);

                    // subsctibe the new socket to Epoll events
                    Add(socketHandle, handler.GetEvents() );

                }
            }
            return 1;
        }

        public int OnWriteReady()
        {
            // no op
            return 0;
        }

        private void disconnect(EpollHandler handler)
        {
            var os = new Overspan(50);

            // detach the handler
            var socketHandle = handler.OnDetach();

            // unsubscribe from Epoll events
            Remove(socketHandle, 0);

            // close the socket
            if( Syscall.close(socketHandle) < 0 )
                Log.Error($"Call to close(socked) failed: {Stdlib.GetLastError()}");
            Log.Epoll("[{0}] socked disconnected, closed", socketHandle);

            os.Check("disconnect()");
        }

        public static int HandlersCreated = 0;
    }
}
