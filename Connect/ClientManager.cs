using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Connect
{
    class ClientManager
    {

        ConnectArgs arguments;
        int messageSize = SocketClient.MessageSize;

        public ClientManager(ConnectArgs arguments)
        {
            this.arguments = arguments;        
        }

        public IDisposable Start()
        {
            Queue<SocketClient> clients = new Queue<SocketClient>(arguments.connectionLimit);
            var connections = new Subject<SocketClient>();
            EventHandler connecthandler = (s, args) =>
            {
                connections.OnNext(s as SocketClient);
            };


            long bytesSent = 0;

            EventHandler<long> onsend = (s, i) =>
            {
                bytesSent += i;
            };

            Action connect = () =>
            {
                SocketClient client = new SocketClient(arguments.server, arguments.port);
                client.OnConnected += connecthandler;
                client.OnSend += onsend;
                client.ConnectAsync();
                lock (clients)
                {
                    clients.Enqueue(client);
                }
            };

            //Ping(clients);

            int count = 0;
            var resource = connections.Subscribe(_ =>
            {
                count++;
                if (count < arguments.connectionLimit)
                {
                    connect();
                }
                else
                {
                    connections.OnCompleted();
                }
            },
                () =>
                {
                    Console.WriteLine("client: Active {0} connections", count);
                    Console.WriteLine("Sending Messages at a rate of {0} messages/sec", arguments.messageRate);
                    Observable.Interval(TimeSpan.FromSeconds(1))
                             .Subscribe(_ =>
                             {
                                 for (int i = 0; i < arguments.messageRate; i++)
                                 {
                                     MessageNextClient(clients);
                                 }
                             });
                });

            connect(); //Kick off the process  
            long previousConnects = 0;
            const int pollTimeSeconds = 2;
            Observable.Interval(TimeSpan.FromSeconds(pollTimeSeconds))
                .TakeWhile(_ => count < arguments.connectionLimit)
                .Subscribe(_ =>
                {
                    var diff = count - previousConnects;
                    Console.WriteLine("client: Active: {0} \tconnects/sec: {1}  \tPending: {2}",
                        count,
                        diff / pollTimeSeconds,
                        arguments.connectionLimit - count);
                    previousConnects = count;
                });


            long previous = 0;
            connections.TakeLast(1).SelectMany(_ => Observable.Interval(TimeSpan.FromSeconds(1)))
              .Subscribe(_ =>
              {
                  var diff = bytesSent - previous;
                  Console.WriteLine("client: Active: {0} \tMsg/sec: {1} \tTransferRate: {2} \tTotalTransfered: {3}",
                      clients.Count,
                      diff / messageSize,
                      diff,
                      bytesSent);
                  previous = bytesSent;
              });

            return resource;
        }

        private void Ping(Queue<SocketClient> clients)
        {
            Observable.Interval(TimeSpan.FromSeconds(1))
                .TakeWhile(_ => clients.Count < arguments.connectionLimit)
                .Subscribe(_ =>
                {
                    int numberOfClients = (int)Math.Ceiling(clients.Count / 60.0);
                    Console.WriteLine("client: Pinging {0} connections", numberOfClients);
                    for (int i = 0; i < numberOfClients; i++)
                    {
                        MessageNextClient(clients);
                    }
                });
        }

        private static void MessageNextClient(Queue<SocketClient> clients)
        {
            SocketClient c = null;
            lock (clients)
            {
                if (clients.Count > 0)
                {
                    c = clients.Dequeue();
                    clients.Enqueue(c);
                }
            }
            if (c != null)
            {
                c.Send();
            }
        }

    }
}
