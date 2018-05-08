using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    class DreyfusWagner
    {
        Graph G;
        List<Edge> forcedEdges;
        public DreyfusWagner(Graph G, List<Edge> forcedEdges)
        {
            this.G = G;
            this.forcedEdges = forcedEdges;
        }
        public void Solve()
        {
            Vertex[] terminals = Vertices.Where((v) => v.IsTerminal).OrderBy((v) => v.Id).ToArray();
            if (terminals.Length > 32) throw new Exception("Only supports up to 32 terminals.");

            G.ComputeDistances();

            int[][] dpTable = new int[n][];
            int[][] parentSet = new int[n][];
            int[][] parentVia = new int[n][];

            for (int i = 0; i < n; i++)
            {
                dpTable[i] = new int[1 << terminals.Length];
                parentSet[i] = new int[1 << terminals.Length];
                parentVia[i] = new int[1 << terminals.Length];
            }

            for (int i = 0; i < n; i++)
                for (int j = 1; j < 1 << terminals.Length; j++)
                    dpTable[i][j] = int.MaxValue;

            List<int> subsets = new List<int>(), newsubsets = new List<int>();

            for (int i = 0; i < terminals.Length; i++)
            {
                subsets.Add(1 << i);
                for (int j = 0; j < n; j++)
                {
                    dpTable[j][1 << i] = G.distance[terminals[i].Id, j];
                    parentVia[j][1 << i] = terminals[i].Id;
                }
            }

            int[] minPartitionCost = new int[1 << terminals.Length];
            int[] bestPartition = new int[1 << terminals.Length];

            int[] terminalPosInSubset = new int[terminals.Length];

            for (int size = 2; size <= terminals.Length; size++)
            {
                for (int i = 0; i < terminals.Length; i++)
                    foreach (int j in subsets)
                        if (1 << i > j)
                            newsubsets.Add(j | (1 << i));
                List<int> temp = newsubsets; newsubsets = subsets; subsets = temp; newsubsets.Clear();

                if (Program.Debug) Console.WriteLine("{0} / {1}", size, terminals.Length);

                for (int via = 0; via < n; via++)
                {
                    foreach (int subset in subsets)
                    {
                        minPartitionCost[subset] = int.MaxValue;

                        int tc = 0;
                        for (int i = 0; i < terminals.Length; i++)
                            if ((subset & (1 << i)) != 0)
                                terminalPosInSubset[tc++] = i;
                        
                        int split = (1 << terminalPosInSubset[size - 1]);
                        int[] curTable = dpTable[via];
                        while (split != subset)
                        {
                            int thisCost = curTable[split] + curTable[subset ^ split];
                            
                            if (thisCost < minPartitionCost[subset])
                            {
                                minPartitionCost[subset] = thisCost;
                                bestPartition[subset] = split;
                            }

                            int pos = 0;
                            while (true)
                            {
                                if ((split & (1 << terminalPosInSubset[pos])) == 0)
                                {
                                    split |= (1 << terminalPosInSubset[pos]);
                                    break;
                                }
                                else
                                {
                                    split ^= (1 << terminalPosInSubset[pos]);
                                    pos++;
                                }
                            }
                        }
                    }

                    // Iterating over an array is much faster than interating over a list
                    int[] subsetsLocal = subsets.ToArray();
                    Array.Sort(subsetsLocal); // Improves the access pattern, might be beneficial since we iterate over this list n times

                    for (int from = 0; from < n; from++)
                    {
                        int[] curTable = dpTable[from];
                        int[] curParentSet = parentSet[from];
                        int[] curParentVia = parentVia[from];
                        int dist = G.distance[from, via];

                        foreach (int subset in subsetsLocal)
                        {
                            if (dist + minPartitionCost[subset] < curTable[subset])
                            {
                                curTable[subset] = dist + minPartitionCost[subset];
                                curParentSet[subset] = bestPartition[subset];
                                curParentVia[subset] = via;
                            }
                        }
                    }
                }
            }

            int bestCost = int.MaxValue; Tuple<int, int> bestParent = Tuple.Create(0, 0); int bestFrom = 0;
            for (int via = 0; via < n; via++)
                if (dpTable[via][(1 << terminals.Length) - 1] < bestCost)
                {
                    bestCost = dpTable[via][(1 << terminals.Length) - 1];
                    bestParent = Tuple.Create(parentSet[via][(1 << terminals.Length) - 1], parentVia[via][(1 << terminals.Length) - 1]);
                    bestFrom = via;
                }

            if (bestCost == int.MaxValue) throw new Exception("No solution.");

            bestCost += forcedEdges.Sum((e) => e.Weight);

            Console.WriteLine("VALUE " + bestCost);

            Reconstruct(bestParent, (1 << terminals.Length) - 1, bestFrom, parentSet, parentVia);

            foreach (Edge e in forcedEdges)
                if (!result.Contains(new Edge(e.To, e.From, 0)) && !result.Contains(e))
                    result.Add(e);

            if (Program.Debug) return;

            foreach (Edge e in result.OrderBy((e) => e.Weight).ThenBy((e) => Math.Min(e.To.Id, e.From.Id)).ThenBy((e) => Math.Max(e.To.Id, e.From.Id)))
                Console.WriteLine(Math.Min((e.From.Id + 1), (e.To.Id + 1)) + " " + Math.Max((e.From.Id + 1), (e.To.Id + 1)));
        }

        HashSet<Edge> result = new HashSet<Edge>();

        public void Reconstruct(Tuple<int, int> next, int subset, int from, int[][] parentSet, int[][] parentVia)
        {
            if (subset == 0) return;

            int leftSet = next.Item1;
            int rightSet = subset ^ leftSet;
            int via = next.Item2;

            Tuple<int, int> nextLeft = Tuple.Create(parentSet[via][leftSet], parentVia[via][leftSet]);
            Tuple<int, int> nextRight = Tuple.Create(parentSet[via][rightSet], parentVia[via][rightSet]);

            if (!nextLeft.Equals(next)) Reconstruct(nextLeft, leftSet, via, parentSet, parentVia);
            if (!nextRight.Equals(next)) Reconstruct(nextRight, rightSet, via, parentSet, parentVia);
            
            Edge? e = G.pred[from, via];
            while(e != null)
            {
                Edge e2 = (Edge)e;
                result.Add(e2);
                e = G.pred[from, e2.From.Id];
            }
        }

        public Vertex[] Vertices
        {
            get
            {
                return G.Vertices;
            }
        }

        public int n
        {
            get
            {
                return Vertices.Length;
            }
        }
    }
}
