using System.Net.Sockets;

namespace Connect
{
    class SocketArgsPool
    {
        const int BufferSize = 256;

        internal static SocketAsyncEventArgs Take()
        {
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[BufferSize], 0, BufferSize);
            return args;
        }
    }
}
