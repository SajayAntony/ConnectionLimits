using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Connect
{
    class SocketClient
    {
        public const int MessageSize = 4;
        volatile bool pending = false;
        public volatile int queued = 0;
        WaitCallback SendCoreHandler;
        public event EventHandler OnConnected;
        public long bytesTransfered = 0;
        public EventHandler<long> OnSend;

        public SocketClient(string host, int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(host);
            IPAddress ipAddress = ipHostInfo.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork).First();
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            this.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.Socket.NoDelay = true;
            this.SendCoreHandler = new WaitCallback(SendCore);
            this.args = new SocketAsyncEventArgs();
            args.Completed += ArgsCallback;
            args.SetBuffer(new byte[MessageSize], 0, MessageSize);
            for (int i = 0; i < MessageSize; i++)
            {
                args.Buffer[i] = (byte)i;
            }

            args.RemoteEndPoint = remoteEP;         
   
            // setup port scalability
            this.Socket.SetSocketOption(SocketOptionLevel.Socket, (SocketOptionName)0x3006, true);
        }

        public void ConnectAsync()
        {
            this.pending = true;
            args.SetBuffer(0, 0);
            if (!Socket.ConnectAsync(args))
            {
                ArgsCallback(null, args);
            }
        }

        void ArgsCallback(object sender, SocketAsyncEventArgs e)
        {
            pending = false;
            this.TerminateProcessOnError();
            if (e.LastOperation == SocketAsyncOperation.Connect)
            {
                args.SetBuffer(0, args.Buffer.Length);
                if (this.OnConnected != null)
                {
                    this.OnConnected(this, EventArgs.Empty);
                }
            }
            else if(e.LastOperation == SocketAsyncOperation.Send)
            {
                Interlocked.Add(ref this.bytesTransfered, e.BytesTransferred);
                if (this.OnSend != null)
                {
                    this.OnSend(this, e.BytesTransferred);
                }

                this.SendCore(null);
            }
        }

        internal void Send()
        {
            this.queued++;
            SendCore(null);
        }

        private void SendCore(object state)
        {
            if (this.queued <= 0)
            {
                return;
            }

            if (!pending)
            {
                lock (this.Socket)
                {
                    if (!pending)
                    {
                        queued--;
                        pending = true;
                        if (!this.Socket.SendAsync(this.args))
                        {
                            this.pending = false;
                            this.TerminateProcessOnError();
                            if (this.queued > 0)
                            {
                                ThreadPool.UnsafeQueueUserWorkItem(this.SendCoreHandler, null);
                            }
                        }
                    }
                }
            }
        }

        private void TerminateProcessOnError()
        {
            if (this.args.SocketError != SocketError.Success)
            {
                Console.WriteLine("Socket error = " + this.args.SocketError);
                Environment.Exit(1);
            }
        }        

        public SocketAsyncEventArgs args { get; set; }

        public Socket Socket { get; set; }
    }
}
