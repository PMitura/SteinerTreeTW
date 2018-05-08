using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    class PriorityQueue<T>
    {
        T[] heap;
        long[] priorities;
        int heapSize;

        public void print()
        {
            for (int i = 0; i < heapSize; i++)
                Console.Write(priorities[i]);
        }

        public PriorityQueue() : this(64) { }

        public PriorityQueue(int size)
        {
            heapSize = 0;
            heap = new T[size];
            priorities = new long[size];
        }

        public T Dequeue()
        {
            if (heapSize <= 0)
                throw new Exception("PQueue empty!");
            T result = heap[0];

            heapSize--;
            heap[0] = heap[heapSize];
            priorities[0] = priorities[heapSize];

            int k = 0;
            while (true)
            {
                int l = 2 * k + 1;
                int r = l + 1;

                if (r < heapSize && priorities[r] < priorities[k] && priorities[l] >= priorities[r])
                    Swap(k, (k = r));
                else if (l < heapSize && priorities[l] < priorities[k])
                    Swap(k, (k = l));
                else
                    break;
            }

            return result;
        }

        public T Peek()
        {
            return heap[0];
        }

        public long PeekDist()
        {
            return priorities[0];
        }

        public void Enqueue(T obj, long priority)
        {
            if (heapSize == heap.Length)
            {
                T[] newHeap = new T[heap.Length * 2];
                long[] newP = new long[heap.Length * 2];

                for (int i = 0; i < heap.Length; i++)
                {
                    newHeap[i] = heap[i];
                    newP[i] = priorities[i];
                }

                priorities = newP;
                heap = newHeap;
            }

            priorities[heapSize] = priority;
            heap[heapSize] = obj;
            int k = heapSize++;

            while (k > 0 && priorities[k] < priorities[(k + 1) / 2 - 1])
                Swap(k, (k = (k + 1) / 2 - 1));
        }

        private void Swap(int a, int b)
        {
            long oldP = priorities[a];
            T oldO = heap[a];
            heap[a] = heap[b];
            priorities[a] = priorities[b];
            heap[b] = oldO;
            priorities[b] = oldP;
        }

        public bool IsEmpty()
        {
            return heapSize == 0;
        }
    }
}
