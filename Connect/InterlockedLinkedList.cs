using System.Diagnostics.Contracts;
using System.Threading;

namespace System.Concurrent
{
    class InterlockedList<T>
    {
        InterlockedListEntry<T> head;

        /// <summary>
        /// Special singleton to denote the end of the list. 
        /// The item's next points to itself so that Removing at the tail will replace 
        /// with the same reference. Multiple threads might try to remove the tail and all succecceed
        /// as it CMPXG of the the same reference into the list head. 
        /// </summary>
        static readonly InterlockedListEntry<T> Tail;

        static InterlockedList()
        {
            Tail = TailListEntry.Instance;
        }

        public InterlockedList()
        {
            this.head = Tail;
        }

        public void Add(T value)
        {
            InterlockedListEntry<T> node = new InterlockedListEntry<T>();
            node.Value = value;
            this.Add(ref node);
        }

        public bool Remove(out T value)
        {
            InterlockedListEntry<T> node;
            if (this.RemoveNode(out node))
            {
                value = node.Value;
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        /// <summary>
        /// Add a node into the list. The function updates the next of the node to the current head
        /// and replace the head with itself. If the head value read was stale then it resets 
        /// the next until it obtains the current value.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        InterlockedListEntry<T> Add(ref InterlockedListEntry<T> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            if (node.Next != null)
            {
                throw new ArgumentException("Node already points to an entry. Ensure that that node does not belong to another list.");
            }

            
            InterlockedListEntry<T> nextSlot = this.head;
            node.Next = nextSlot;

            while (true)
            {
                if (nextSlot == (nextSlot = Interlocked.CompareExchange(ref this.head, node, nextSlot)))
                {
                    return this.head;
                }
                else
                {
                    // Set the next only in the failed case. The node could have been already removed. 
                    // and node.next could have become null and we don't want to overwrite it in that case. 
                    node.Link(ref nextSlot);
                }
            }
        }

        /// <summary>
        /// Removes a node from the list and sets the head to its next pointer. 
        /// If the remove reads a tail value then it returns false indicating that there is 
        /// no value in the list. 
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        bool RemoveNode(out InterlockedListEntry<T> node)
        {
            node = this.head;

            while (true)
            {
                if (node == (node = Interlocked.CompareExchange(ref this.head, node.Next, node)))
                {
                    if (node != Tail)
                    {
                        node.Unlink();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Container for the Interlocked list value.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        class InterlockedListEntry<U>
        {
            public U Value { get; set; }

            public InterlockedListEntry<U> Next { get; private set; }

            public void Unlink()
            {
                this.Next = null;
            }

            public void Link(ref InterlockedListEntry<U> next)
            {
                this.Next = next;
            }
        }

        /// <summary>
        /// Special non instatiatable class. 
        /// </summary>
        class TailListEntry : InterlockedListEntry<T>
        {
            public static TailListEntry Instance;

            static TailListEntry()            
            {
                Instance = new TailListEntry();                
            }

            private TailListEntry()
            {
                this.Link(ref this);
            }
        }
    }
}
