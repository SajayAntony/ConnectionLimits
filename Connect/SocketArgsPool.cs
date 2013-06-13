using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

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
