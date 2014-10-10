using CmdLine;
using System;
using System.Diagnostics;
using System.Net;
using System.Reactive.Linq;
using System.Threading;

namespace Connect
{
    class Program
    {
        static void Main(string[] args)
        {
            SetupThreadPool();

            try
            {
                Console.WriteLine("PID:" + Process.GetCurrentProcess().Id);
                var arguments = CommandLine.Parse<ConnectArgs>();
                if (System.String.Compare(arguments.Mode, "server", System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    StartServer(arguments);
                    CommandLine.Pause();
                }
                else if (System.String.Compare(arguments.Mode, "client", System.StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var clients = new ClientManager(arguments);
                    clients.Start();
                    CommandLine.Pause();
                }
                else
                {
                    arguments.Server = Dns.GetHostName();
                    StartServer(arguments);
                    var clients = new ClientManager(arguments);
                    clients.Start();
                    CommandLine.Pause();
                }
            }
            catch (CommandLineException exception)
            {
                Console.WriteLine(exception.ArgumentHelp.Message);
                Console.WriteLine(exception.ArgumentHelp.GetHelpText(Console.BufferWidth));
            }
        }

        private static void SetupThreadPool()
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext.SuppressFlow();
            }

            ThreadPool.SetMinThreads(500, 1000);
        }

        private static void StartServer(ConnectArgs arguments)
        {
            var server = new SocketServer(arguments.Port);
            var connectionCount = 0;
            var messageCount = 0;
            server.Listener.OnAccept().Subscribe((s) =>
            {
                Interlocked.Increment(ref connectionCount);
                s.OnMessage().Subscribe((d) => Interlocked.Increment(ref messageCount));
            });

            Observable.Interval(TimeSpan.FromSeconds(5)).TimeInterval()
                .Subscribe(_ =>
                {
                    var rate = Interlocked.Exchange(ref messageCount, 0) / 5.0;
                    var current = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("server: Active: ");
                    Console.Write(connectionCount);
                    Console.Write("\tMsg/sec: ");
                    Console.WriteLine(rate);
                    Console.ForegroundColor = current;
                });
        }
    }
}
