﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Connect
{
    class ConnectionAcceptor
    {
        public IObservable<Socket> Start(Socket listenSocket)
        {
            throw new NotImplementedException();
        }
    }

    class AcceptArgsPool
    {
        // Allocate into the LOH
        const int BufferSize = 85000;
        byte[] zeroAcceptBuffer = new byte[BufferSize];
        ConcurrentQueue<ArraySegment<Byte>> bufferList;
        private GCHandle handle;
        const int BufferCount = 10;
        const int SingleBufferCount = 4;

        public AcceptArgsPool()
        {
            //Pin the buffer;
            this.handle = GCHandle.Alloc(zeroAcceptBuffer);

            for (int i = 0; i < BufferCount; i++)
            {
                var buffer = new ArraySegment<byte>(zeroAcceptBuffer, i * SingleBufferCount, SingleBufferCount);
                bufferList.Enqueue(buffer);
            }
        }

        ArraySegment<byte> Take()
        {
            ArraySegment<byte> buffer;
            if (bufferList.TryDequeue(out buffer))
            {
                return buffer;
            }

            throw new InvalidOperationException("We should never run out of accept buffers in the pool.");
        }

        void Return(ref ArraySegment<Byte> item)
        {
            bufferList.Enqueue(item);
        }
    }
}
