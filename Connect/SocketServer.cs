using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Connect
{
    class SocketServer : IDisposable
    {
        public Socket Listener { get; private set; }

        public SocketServer(int port)
        {
            this.Listener = Start(port);
        }

        private static Socket Start(int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);

            Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, 0);
            listener.Bind(localEndPoint);
            listener.Listen(500);
            var ep = listener.LocalEndPoint;
            Console.WriteLine("Listening on " + ep.ToString());
            return listener;
        }


        ~SocketServer()
        {
            Dispose(false);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Listener.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }


    public static class SocketExtensions
    {
        public static IObservable<Socket> OnAccept(this Socket listenerSocket)
        {
            var acceptor = new Subject<Socket>();
            Func<AsyncCallback, object, IAsyncResult> begin = (callback, state) =>
                {
                    return listenerSocket.BeginAccept(callback, state);
                };

            Func<IAsyncResult, Socket> end = (iar) =>
            {
                try
                {
                    var socket = listenerSocket.EndAccept(iar);
                    return socket;
                }
                catch (Exception)
                {
                    Environment.Exit(1);
                }

                return null;
            };

            Func<Task<Socket>> acceptTask = () => Task.Factory.FromAsync(begin, end, null);

            for (int i = 0; i < 10; i++)
            {
                Observable.FromAsync(acceptTask)
                    .Repeat()
                    .Subscribe(s =>
                    {
                        acceptor.OnNext(s);
                    });
            }

            return acceptor;
        }

        public static IObservable<Unit> OnMessage(this Socket clientSocket)
        {
            return Observable.Defer(() => new MessagePump(clientSocket));
        }

        class MessagePump : IObservable<Unit>
        {
            private SocketAsyncEventArgs args;
            private IObserver<Unit> observer;
            public Socket socket { get; set; }
            static int indexer = 0;
            int index = indexer++;
            bool ignoreReset = true;

            public MessagePump(Socket socket)
            {
                this.args = SocketArgsPool.Take();
                this.args.UserToken = this;
                this.args.Completed += args_Completed;
                this.socket = socket;
            }

            bool ReceiveCore()
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                }
                if (this.socket.ReceiveAsync(this.args))
                {
                    return false;
                }

                return true;
            }

            void CompleteReceive()
            {
                while (true)
                {
                    TerminateOnError(this.args);
                    if (this.args.LastOperation == SocketAsyncOperation.Receive)
                    {
                        if (this.args.BytesTransferred > 0)
                        {
                            this.observer.OnNext(Unit.Default);
                        }
                    }

                    if (ReceiveCore())
                    {
                        continue;
                    }

                    break;
                }
            }

            static void args_Completed(object sender, SocketAsyncEventArgs e)
            {
                MessagePump pump = e.UserToken as MessagePump;
                pump.CompleteReceive();
            }

            private void TerminateOnError(SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    if (e.SocketError == SocketError.ConnectionReset && ignoreReset)
                    {
                        ignoreReset = false;
                        Console.WriteLine("Connection reset received from " + socket.RemoteEndPoint.ToString());
                        return;
                    }
                    Console.WriteLine("Socket Error : " + e.SocketError);
                    Environment.Exit(1);
                }
            }

            public IDisposable Subscribe(IObserver<Unit> observer)
            {
                this.observer = observer;

                ThreadPool.UnsafeQueueUserWorkItem((s) =>
                {
                    MessagePump p = s as MessagePump;
                    p.CompleteReceive();
                }, this);
                return Disposable.Create(() =>
                    {
                        this.socket.Dispose();
                        this.args.Dispose();
                    });
            }
        }
    }
}
