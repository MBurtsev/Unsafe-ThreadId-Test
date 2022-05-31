// Maksim Burtsev https://github.com/MBurtsev
// Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DataflowChannel
{
    /// <summary>
    /// MPOC - Multiple Producer One Consumer.
    /// At the core a cycle buffer that implements a producer-consumer pattern. 
    /// Producers use a spinlock for an initialization thread only once at a time.
    /// All read-write operations fully lock-free\wait-free.
    /// No order means that read order is not equal to write order.
    /// </summary>
    public partial class ChannelWithThreadLocal<T>
    {
        // The default value that is used if the user has not specified a capacity.
        private const int SEGMENT_CAPACITY = 32 * 1024;
        // Channel data
        private ChannelData _channel;

        public ChannelWithThreadLocal() : this(SEGMENT_CAPACITY)
        { 
        }

        public ChannelWithThreadLocal(int capacity)
        {
            _channel  = new ChannelData();
        }

        public unsafe void Write(T value)
        {
            unchecked
            {
                var channel = _channel;
                var buffer  = channel.WriterLinks.Value;

                if (buffer == null)
                {
                    buffer = SetupThread(ref channel);
                }

                var pos = buffer.WriterPosition;

                if (pos == SEGMENT_CAPACITY)
                {
                    SetNextSegment(channel, value);

                    return;
                }

                buffer.WriterMessages[pos] = value;
                buffer.WriterPosition = pos + 1;
            }
        }

        private void SetNextSegment(ChannelData channel, T value)
        {
            unchecked
            {
                var buffer  = channel.Storage.Value;
                var seg = buffer.Writer;
                CycleBufferSegment next;

                var flag = seg.Next == null;
                var reader = buffer.Reader;

                if (!flag && seg.Next != reader)
                {
                    next = seg.Next;
                }
                else if (flag && buffer.Head != reader)
                {
                    next = buffer.Head;
                }
                else
                {
                    next = new CycleBufferSegment()
                    {
                        Next = seg.Next
                    };

                    seg.Next = next;
                }

                var link = channel.WriterLinks.Value = next.WriterLink;

                link.WriterMessages[0] = value;
                link.WriterPosition = 1;
                buffer.Writer = next;
            }
        }

        private WriterLink SetupThread(ref ChannelData channel)
        {
            // set lock
            while (Interlocked.CompareExchange(ref channel.SyncChannel, 1, 0) != 0)
            {
                Thread.Yield();
                channel = Volatile.Read(ref _channel);
            }

            try
            {
                var buffer = new CycleBuffer();
                var link = buffer.Writer.WriterLink;

                channel.Storage.Value = buffer;
                channel.WriterLinks.Value = link;

                if (buffer.Head == null)
                {
                    channel.Reader = buffer;
                }

                buffer.Next  = channel.Head;
                channel.Head = buffer;

                return link;
            }
            finally
            {
                // unlock
                channel.SyncChannel = 0;
            }
        }

        #region ' Structures '

        private sealed class ChannelData
        {
            public ChannelData()
            {
                Storage = new ThreadLocal<CycleBuffer>();
                WriterLinks = new ThreadLocal<WriterLink>();
                Head    = null;
                Reader  = null;
            }

            // Head of linked list for reader
            public CycleBuffer Head;
            // Current reader position
            public CycleBuffer Reader;
            // To synchronize threads when expanding the storage or setup new thread
            public int SyncChannel;
            // For writers
            public readonly ThreadLocal<CycleBuffer> Storage;
            //
            public readonly ThreadLocal<WriterLink> WriterLinks;
        }

        private sealed class CycleBuffer
        {
            public CycleBuffer()
            {
                var seg = new CycleBufferSegment();

                Head   = seg;
                Reader = seg;
                Writer = seg;
                Next   = null;
            }

            // Current reader segment
            public CycleBufferSegment Reader;
            // Current writer segment
            public CycleBufferSegment Writer;
            // Head segment
            public CycleBufferSegment Head;
            // Next thread data segment
            public CycleBuffer Next;
        }

        [DebuggerDisplay("Reader:{ReaderPosition}")]
        private sealed class CycleBufferSegment
        {
            public CycleBufferSegment()
            {
                var messages = new T[SEGMENT_CAPACITY];

                Messages = messages;
                WriterLink = new WriterLink(messages);
            }

            // Reading thread position
            public int ReaderPosition;

            public readonly T[] Messages;
            // 
            public readonly WriterLink WriterLink;
            // Next segment
            public CycleBufferSegment Next;
        }

        private sealed class WriterLink
        {
            public WriterLink(T[] messages)
            {
                WriterMessages = messages;
            }

            // Writing thread position
            public int WriterPosition;
            // Current writer segment
            public readonly T[] WriterMessages;
        }

        #endregion
    }
}