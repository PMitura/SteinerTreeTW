using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Numerics;

namespace SteinerTreeTW
{
    class RankBased
    {
        Vertex[] vertices;

        public static int VectorLength = 32 * Vector<uint>.Count;
        public int rowCount, rowCountVector;

        int elimCount; // Stores the number of distinct rows which can currently be eliminated
        int[] elimOrder; // Stores the order in which column entries should be eliminated
        Vector<uint>[][] basisColumns; // Position i stores a column such that inputColumns[elimOrder[i]] is 1, and inputColumns[elimOrder[j]] is 0 for any j < i
        Vector<uint>[][] inputColumns; // Stores columns of the matrix

        int[] orderEpos, orderErem; uint[] orderEmask; // Stores precomputed values to speed up GetBit

        uint[] tempArray; // Temporary array used to generate column
        public RankBased(VertexSubset subset)
        {
            vertices = new Vertex[subset.LocalSubset.BitCount()];
            int pos = 0;
            foreach (Vertex v in subset.ParentBag.Bag)
                if ((subset.LocalSubset & (1 << v.Color)) != 0)
                    vertices[pos++] = v;

            if (TW < vertices.Length - 1)
            {
                TW = vertices.Length - 1;
                arrayPool.Clear();
                inUsePool.Clear();
            }

            rowCount = 1 << (vertices.Length - 1);
            if (rowCount < 1)
                rowCount = 1;
            rowCountVector = (rowCount + VectorLength - 1) / VectorLength;

            elimOrder = new int[rowCount];
            basisColumns = new Vector<uint>[rowCount][];
            elimCount = 0;

            orderEpos = new int[rowCount];
            orderErem = new int[rowCount];
            orderEmask = new uint[rowCount];

            tempArray = new uint[rowCountVector * Vector<uint>.Count];

            inputColumns = new Vector<uint>[rowCount][];
        }

        public Vector<uint>[] GetColumn(PartialSolution s)
        {
            // Generate cuts
            for (int j = 0; j < tempArray.Length; j++)
                tempArray[j] = 0;

            if (vertices.Length <= 1)
                tempArray[0] = 1;
            else
                GenerateCuts(0, 1, s, tempArray, vertices);

            // Copy to SIMD vector
            Vector<uint>[] column = GetArray();
            for (int j = 0; j < rowCountVector; j++)
                column[j] = new Vector<uint>(tempArray, j * Vector<uint>.Count);

            Program.GenerateCount++;

            return column;
        }

        // Adds a row to the current matrix
        // Returns false if s is already represented in the current matrix, returns true otherwise
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddSolution(PartialSolution s)
        {
            return AddSolution(s, GetColumn(s));
        }

        // Adds a row to the current matrix
        // Returns false if s is already represented in the current matrix, returns true otherwise
        // Uses the predefined column to check if the solution should be added to the basis (should be equal to the output of GetColumn(s))
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddSolution(PartialSolution s, Vector<uint>[] currentColumn)
        {
            // Do elimination
            for (int j = 0; j < elimCount; j++)
                //if (GetBit(currentColumn, elimOrder[j]) != 0)
                if ((currentColumn[orderEpos[j]][orderErem[j]] & orderEmask[j]) != 0)
                    XOR(currentColumn, basisColumns[elimOrder[j]], currentColumn, rowCountVector);

            int zeroPos = FirstNonZero(currentColumn, rowCountVector);

            // Column was redundant
            if (zeroPos == -1)
                return false;

            // Do precomputation of (expensive) GetBit to speed things up *a lot*
            orderEpos[elimCount] = zeroPos / VectorLength;
            orderErem[elimCount] = (zeroPos % VectorLength) >> 5;
            orderEmask[elimCount] = 1u << ((zeroPos % VectorLength) & 31);

            // Add to basis
            elimOrder[elimCount++] = zeroPos;
            basisColumns[zeroPos] = currentColumn;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void XOR(Vector<uint>[] a, Vector<uint>[] b, Vector<uint>[] result, int length)
        {
            Program.XORCount++;
            for (int i = 0; i < length; i++)
                result[i] = a[i] ^ b[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AND(Vector<uint>[] a, Vector<uint>[] b, Vector<uint>[] result, int length)
        {
            for (int i = 0; i < length; i++)
                result[i] = a[i] & b[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VectorCopy(Vector<uint>[] source, Vector<uint>[] target, int length)
        {
            for (int i = 0; i < length; i++)
                target[i] = source[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FirstNonZero(Vector<uint>[] a, int length)
        {
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < Vector<uint>.Count; j++)
                    if (a[i][j] != 0)
                    {
                        for (int k = 0; k < 32; k++)
                        {
                            if ((a[i][j] & (1u << k)) != 0)
                                return i * VectorLength + (j << 5) + k;
                        }
                    }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint GetBit(Vector<uint>[] a, int pos)
        {
            int elementPos = pos / VectorLength;
            int elementRem = pos % VectorLength;

            return a[elementPos][elementRem >> 5] & (uint)1 << (elementRem & 31);
        }

        static int[] RootIndex = new int[32];
        static uint[] VerticesInPartition = new uint[33]; // One senitel element
        static void GenerateCuts(int cutIndex, int pos, PartialSolution solution, uint[] column, Vertex[] vertices)
        {
            int partitionChecker = 0; int partitionCount = 0; int zeroID = solution.Find(vertices[0].Color);

            for (int i = 1; i < vertices.Length; i++)
            {
                int id = solution.Find(vertices[i].Color);
                if (id == zeroID) continue; // The connected component containing vertex 0 is fixed (representative is always lowest vertex)

                int mask = 1 << id;
                if ((partitionChecker & mask) == 0)
                {
                    partitionChecker |= mask;
                    RootIndex[id] = partitionCount++;
                    VerticesInPartition[RootIndex[id]] = 1u << (i - 1);
                }
                else
                    VerticesInPartition[RootIndex[id]] |= 1u << (i - 1);
            }

            uint cut = 0;

            for (uint i = 0; i < (1 << partitionCount); i++)
            {
                uint delta = i ^ (i + 1);
                column[cut >> 5] |= 1u << (int)(cut & 31);
                int j = 0;
                while (delta != 0)
                {
                    cut ^= VerticesInPartition[j];
                    j++;
                    delta >>= 1;
                }
            }
        }

        // Old, slower, recursive method of generating a cut
        void GenerateCuts_old(int cutIndex, int pos, PartialSolution solution, uint[] column, Vertex[] vertices)
        {
            if (pos >= vertices.Length)
            {
                column[cutIndex >> 5] |= 1u << (cutIndex & 31);
                return;
            }

            bool canGoLeft = true, canGoRight = true;

            if (solution.Find(vertices[0].Color) == solution.Find(vertices[pos].Color))
                canGoRight = false;

            for (int i = 1; i < pos; i++)
            {
                if (solution.Find(vertices[i].Color) == solution.Find(vertices[pos].Color))
                {
                    if ((cutIndex & (1 << (i - 1))) > 0)
                        canGoLeft = false;
                    else
                        canGoRight = false;
                }
            }

            if (canGoLeft)
                GenerateCuts_old(cutIndex, pos + 1, solution, column, vertices);
            if (canGoRight)
                GenerateCuts_old(cutIndex | (1 << (pos - 1)), pos + 1, solution, column, vertices);
        }

        static int TW = 10;
        static Stack<Vector<uint>[]> arrayPool = new Stack<Vector<uint>[]>(), inUsePool = new Stack<Vector<uint>[]>();
        public static int MaxVectorElements
        {
            get
            {
                return ((1 << TW) + VectorLength - 1) / VectorLength;
            }
        }
        public static Vector<uint>[] GetArray()
        {
            Vector<uint>[] newArray;

            if (arrayPool.Count > 0)

                newArray = arrayPool.Pop();
            else
                newArray = new Vector<uint>[MaxVectorElements];

            inUsePool.Push(newArray);

            return newArray;
        }

        public static void ClearPool()
        {
            while (inUsePool.Count > 0)
                arrayPool.Push(inUsePool.Pop());
        }

        // Releases an array back to the pool, but only if it is the most recently given out one
        public static void ReleaseArray(Vector<uint>[] arr)
        {
            if (inUsePool.Peek() == arr)
                arrayPool.Push(inUsePool.Pop());
        }

        public static Dictionary<PartialSolution, int> Reduce(Dictionary<PartialSolution, int> table)
        {
            return Reduce(table, 0.25);
        }
        public static Dictionary<PartialSolution, int> Reduce(Dictionary<PartialSolution, int> table, double tr)
        {
            //return Reduce_M4R(table, tr);

            // No point further reducing something empty
            if (!table.Any() || table.First().Key.Subset == null) return table;

            int nv = table.First().Key.Subset.LocalSubset.BitCount();

            // Not worth reducing
            if (table.Count * tr <= (1 << (nv - 1)))
                return table;

            Dictionary<PartialSolution, int> result = new Dictionary<PartialSolution, int>();

            if (nv <= 1)
            {
                KeyValuePair<PartialSolution, int> minSol = table.Where((s) => s.Value == table.Values.Min()).First();
                result.Add(minSol.Key, minSol.Value);
                return result;
            }

            RankBased irb = new RankBased(table.First().Key.Subset);
            foreach (KeyValuePair<PartialSolution, int> kvp in table.OrderBy((s) => s.Value))
                if (irb.AddSolution(kvp.Key))
                    result.Add(kvp.Key, kvp.Value);

            RankBased.ClearPool();

            return result;
        }
        public static Dictionary<PartialSolution, int> Reduce_M4R(Dictionary<PartialSolution, int> table)
        {
            return Reduce_M4R(table, 0.25);
        }

        public static Dictionary<PartialSolution, int> Reduce_M4R(Dictionary<PartialSolution, int> table, double tr)
        {
            // No point further reducing something empty
            if (!table.Any() || table.First().Key.Subset == null) return table;

            int nv = table.First().Key.Subset.LocalSubset.BitCount();

            // Not worth reducing
            if (table.Count * tr <= (1 << (nv - 1)))
                return table;

            Dictionary<PartialSolution, int> result;

            if (nv <= 1)
            {
                KeyValuePair<PartialSolution, int> minSol = table.Where((s) => s.Value == table.Values.Min()).First();
                result = new Dictionary<PartialSolution, int>();
                result.Add(minSol.Key, minSol.Value);
                return result;
            }

            RankBased irb = new RankBased(table.First().Key.Subset);

            result = irb.M4RReduce(table);

            RankBased.ClearPool();

            return result;
        }

        static List<Vector<uint>[]>[] buckets = new List<Vector<uint>[]>[1 << 5];

        // Reduce method, but using Method of Four Russians
        Dictionary<PartialSolution, int> M4RReduce(Dictionary<PartialSolution, int> table)
        {
            Dictionary<PartialSolution, int> result = new Dictionary<PartialSolution, int>();
            List<KeyValuePair<PartialSolution, int>> sortedInput = table.OrderBy((x) => x.Value).ToList();

            // Compute columns
            Vector<uint>[][] inputColumns = sortedInput.Select((kvp) => GetColumn(kvp.Key)).ToArray();

            int elimStartFrom = 0;

            int[] bucketIndex = new int[inputColumns.Length];

            for (int i = 0; i < buckets.Length; i++)
                buckets[i] = new List<Vector<uint>[]>();

            Vector<uint>[] zeroVector = GetArray();
            for (int i = 0; i < rowCountVector; i++)
                zeroVector[i] = Vector<uint>.Zero;
            Vector<uint>[] tempVector = GetArray();

            for (int i = 0; i < inputColumns.Length; i++)
            {
                // Eliminate upto the point we haven't done yet
                for (int j = elimStartFrom; j < elimCount; j++)
                    if ((inputColumns[i][orderEpos[j]][orderErem[j]] & orderEmask[j]) != 0)
                        XOR(inputColumns[i], basisColumns[elimOrder[j]], inputColumns[i], rowCountVector);

                int zeroPos = FirstNonZero(inputColumns[i], rowCountVector);

                if (zeroPos == -1)
                    continue;

                result.Add(sortedInput[i].Key, sortedInput[i].Value);

                // Do precomputation of (expensive) GetBit to speed things up *a lot*
                orderEpos[elimCount] = zeroPos / VectorLength;
                orderErem[elimCount] = (zeroPos % VectorLength) >> 5;
                orderEmask[elimCount] = 1u << ((zeroPos % VectorLength) & 31);

                // Add to basis
                elimOrder[elimCount++] = zeroPos;
                basisColumns[zeroPos] = inputColumns[i];

                //int cost = 1 << (elimCount - elimStartFrom + 4);

                //if (cost * cost > inputColumns.Length - i || elimCount - elimStartFrom >= 10)// && (inputColumns.Length - i) > 1024)
                if(elimCount - elimStartFrom == 5 && inputColumns.Length - i > 512)
                {
                    int count = elimCount - elimStartFrom;

                    // Clean up basis
                    for(int j = elimCount - 2; j >= elimStartFrom; j--)
                    {
                        for (int k = elimCount - 1; k > j; k--)
                        {
                            if ((basisColumns[elimOrder[j]][orderEpos[k]][orderErem[k]] & orderEmask[k]) != 0)
                                XOR(basisColumns[elimOrder[j]], basisColumns[elimOrder[k]], basisColumns[elimOrder[j]], rowCountVector);
                        }
                    }

                    for (int j = i + 1; j < inputColumns.Length; j++)
                        for (int bit = elimStartFrom; bit < elimCount; bit++)
                        {
                            if ((inputColumns[j][orderEpos[bit]][orderErem[bit]] & orderEmask[bit]) != 0)
                                bucketIndex[j] |= 1 << bit - elimStartFrom;
                        }

                    for (int j = i + 1; j < inputColumns.Length; j++)
                    {
                        buckets[bucketIndex[j]].Add(inputColumns[j]);
                        bucketIndex[j] = 0;
                    }

                    VectorCopy(zeroVector, tempVector, rowCountVector);

                    int prev = 0;

                    for (int j = 0; j < (1 << count); j++)
                    {
                        if (buckets[j].Count == 0) continue;

                        int delta = j ^ (prev);
                        int pos = elimStartFrom;
                        while (delta != 0 && pos < elimCount)
                        {
                            if((delta & 1) != 0)
                                XOR(basisColumns[elimOrder[pos]], tempVector, tempVector, rowCountVector);

                            pos++;
                            delta >>= 1;
                        }
                        prev = j;

                        foreach (Vector<uint>[] vect in buckets[j])
                            XOR(tempVector, vect, vect, rowCountVector);
                        buckets[j].Clear();
                    }

                    elimStartFrom = elimCount;
                }
            }

            return result;
        }
    }
}
