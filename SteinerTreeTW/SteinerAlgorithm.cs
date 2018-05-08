using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Diagnostics;

namespace SteinerTreeTW
{
    partial class SteinerAlgorithm : IDPAlgorithm<Dictionary<int, Dictionary<PartialSolution, int>>>
    {
        public SteinerAlgorithm()
        {

        }

        Stopwatch introSW = new Stopwatch(), forgetSW = new Stopwatch(), joinSW = new Stopwatch();
        public void Diagnostics()
        {
            Console.WriteLine("Intro: {0}, Forget: {1}, Join: {2}", Math.Round(introSW.ElapsedMilliseconds / 1000.0, 2), Math.Round(forgetSW.ElapsedMilliseconds / 1000.0, 2), Math.Round(joinSW.ElapsedMilliseconds / 1000.0, 2));
        }
        public Dictionary<int, Dictionary<PartialSolution, int>> Leaf(int width)
        {
            Dictionary<int, Dictionary<PartialSolution, int>> result = new Dictionary<int, Dictionary<PartialSolution, int>>();
            Dictionary<PartialSolution, int> dict = new Dictionary<PartialSolution, int>();
            dict.Add(new PartialSolution(width), 0);
            result.Add(0, dict);
            return result;
        }
        public Dictionary<int, Dictionary<PartialSolution, int>> Forget(TDNode bag, Vertex[] vertices, Dictionary<int, Dictionary<PartialSolution, int>> table)
        {
            forgetSW.Start();

            Dictionary<int, Dictionary<PartialSolution, int>> result = new Dictionary<int, Dictionary<PartialSolution, int>>();
            
            int vertexMask = 0;
            foreach (Vertex v in vertices)
                vertexMask |= (1 << v.Color);

            byte[] tempUF = new byte[bag.ParentDecomposition.Width];

            foreach (KeyValuePair<int, Dictionary<PartialSolution, int>> kvp in table)
            {
                int subset = kvp.Key;
                int newSubset = subset & ~vertexMask;
                Dictionary<PartialSolution, int> newDict = null;
                if (!result.TryGetValue(newSubset, out newDict))
                {
                    newDict = new Dictionary<PartialSolution, int>();
                    result.Add(newSubset, newDict);
                }

                foreach (KeyValuePair<PartialSolution, int> kvp2 in kvp.Value)
                {
                    if (kvp2.Key.CountComponents() != kvp2.Key.CountComponents(newSubset)) continue; // Check whether set remains connected
                    VertexSubset newVertexSubset = VertexSubset.Create(bag, newSubset, kvp2.Key.Subset, null);
                    PartialSolution newPs = new PartialSolution(newVertexSubset, kvp2.Key);
                    Upsert(newDict, newPs, kvp2.Value);
                }
            }

            foreach (KeyValuePair<int, Dictionary<PartialSolution, int>> kvp in table.ToList())
            {
                table[kvp.Key] = RankBased.Reduce(kvp.Value, 0.125);
            }

            forgetSW.Stop();

            Program.TableCount += result.Values.Sum((t) => t.Count);

            return result;
        }

        public Dictionary<int, Dictionary<PartialSolution, int>> Introduce(TDNode bag, Vertex[] vertices, Dictionary<int, Dictionary<PartialSolution, int>> table)
        {
            introSW.Start();

            Vertex[] terminals = vertices.Where((v) => v.IsTerminal).ToArray();
            Vertex[] nonterminals = vertices.Where((v) => !v.IsTerminal).ToArray();
            
            Dictionary<int, Dictionary<PartialSolution, int>> result = new Dictionary<int, Dictionary<PartialSolution, int>>();

            for (int i = 0; i < (1 << nonterminals.Length); i++)
            {
                List<Vertex> toAdd = new List<Vertex>(terminals.Length + i.BitCount());
                foreach (Vertex v in terminals) toAdd.Add(v);
                for (int j = 0; j < nonterminals.Length; j++)
                    if ((i & (1 << j)) != 0)
                        toAdd.Add(nonterminals[j]);

                int addMask = 0;
                foreach (Vertex v in toAdd)
                    addMask |= (1 << v.Color);

                List<Edge> introEdges = new List<Edge>();
                foreach (Vertex v in toAdd)
                {
                    foreach (Edge e in v.Adj)
                        if (bag.Bag.Contains(e.To) && !introEdges.Contains(e))
                            introEdges.Add(e);
                }

                foreach (KeyValuePair<int, Dictionary<PartialSolution, int>> kvp in table)
                {
                    int subset = kvp.Key;
                    int newSubset = subset | addMask;
                    Dictionary<PartialSolution, int> newDict = new Dictionary<PartialSolution, int>();

                    Dictionary<PartialSolution, int> localTable = RankBased.Reduce(kvp.Value, 0.999);

                    foreach (KeyValuePair<PartialSolution, int> kvp2 in localTable)
                    {
                        VertexSubset newVertexSubset = VertexSubset.Create(bag, newSubset, kvp2.Key.Subset, null);
                        newDict.Add(new PartialSolution(newVertexSubset, kvp2.Key), kvp2.Value);
                    }

                    if(introEdges.Where((e) => (newSubset & (1 << e.To.Color)) != 0).Count() > 2)
                    {
                        // Efficient method of introducing multiple edges at once and simultaneously running RankBased Reduce
                        //newDict = IntroduceEdgesIntoTable(newDict, introEdges.Where((e) => (newSubset & (1 << e.To.Color)) != 0).ToArray());
                        newDict = IntroduceEdgesIntoTable(newDict, introEdges.Where((e) => (newSubset & (1 << e.To.Color)) != 0).ToArray());
                    }
                    else
                    {
                        foreach (Edge e in introEdges)
                            if ((newSubset & (1 << e.To.Color)) != 0)
                                newDict = RankBased.Reduce(IntroduceEdge(newDict, e), 0.125);
                    }

                    if (newDict.Count > 0)
                        result.Add(newSubset, newDict);
                }
            }

            introSW.Stop();

            Program.TableCount += result.Values.Sum((t) => t.Count);

            return result;
        }
        public Dictionary<int, Dictionary<PartialSolution, int>> IntroduceEdges(Dictionary<int, Dictionary<PartialSolution, int>> table, List<Edge> edges)
        {
            return table; // Do nothing, use original strategy of introducing edges on introduce vertex for now

            Dictionary<int, Dictionary<PartialSolution, int>> result = new Dictionary<int, Dictionary<PartialSolution, int>>();

            foreach (KeyValuePair<int, Dictionary<PartialSolution, int>> kvp in table)
                result[kvp.Key] = IntroduceEdgesIntoTable(kvp.Value, edges.Where((e) => (kvp.Key & (1 << e.To.Color)) != 0 && (kvp.Key & (1 << e.From.Color)) != 0).ToArray());

            return result;
        }

        public Dictionary<PartialSolution, int> IntroduceEdgesIntoTable(Dictionary<PartialSolution, int> table, Edge[] edges)
        {
            if (table.Count == 0 || edges.Length == 0) return table;

            Dictionary<PartialSolution, int> result = new Dictionary<PartialSolution, int>();
            RankBased irb = new RankBased(table.First().Key.Subset);

            PriorityQueue<PartialSolution> pq = new PriorityQueue<PartialSolution>(table.Count * 2);
            foreach (KeyValuePair<PartialSolution, int> kvp in table)
                pq.Enqueue(kvp.Key, kvp.Value);

            HashSet<PartialSolution> seen = new HashSet<PartialSolution>();

            while (!pq.IsEmpty())
            {
                int weight = (int)pq.PeekDist();
                PartialSolution ps = pq.Dequeue();

                // HashSet is faster than RankBased
                if (!seen.Add(ps)) continue;

                // Solution is already represented by output
                if (!irb.AddSolution(ps)) continue;


                result.Add(ps, weight);
                Upsert(result, ps, weight);

                // Add edges to ps. Note that adding edges is not necessary if solution is already represented (since we could have added the same edges to the representatives instead)
                foreach (Edge e in edges)
                {
                    if (ps.Find(e.To.Color) == ps.Find(e.From.Color))
                        continue;
                    PartialSolution newSol = new PartialSolution(ps.Subset, ps);
                    newSol.Union(e.To.Color, e.From.Color);
                    pq.Enqueue(newSol, weight + e.Weight);
                }
            }

            RankBased.ClearPool();

            return result;
        }
        public Dictionary<PartialSolution, int> IntroduceEdge(Dictionary<PartialSolution, int> table, Edge e)
        {
            foreach(KeyValuePair<PartialSolution, int> kvp in table.ToList())
            {
                if (kvp.Key.Find(e.To.Color) == kvp.Key.Find(e.From.Color))
                    continue;
                PartialSolution newSol = new PartialSolution(kvp.Key.Subset, kvp.Key);
                newSol.Union(e.To.Color, e.From.Color);
                Upsert(table, newSol, kvp.Value + e.Weight);
            }

            return table;
        }

        // Joins two tables that have partial solutions with one type of vertex subset
        public Dictionary<PartialSolution, int> JoinTwo(TDNode bag, List<int> verticesInvolved, Dictionary<PartialSolution, int> leftTable, Dictionary<PartialSolution, int> rightTable)
        {
            Dictionary<PartialSolution, int> newTable = new Dictionary<PartialSolution, int>();

            // Reducing before Join is almost certainly beneficial, use a low threshold
            leftTable = RankBased.Reduce(leftTable, 0.999);
            rightTable = RankBased.Reduce(rightTable, 0.999);

            KeyValuePair<PartialSolution, int>[] rightCopy = rightTable.ToArray();

            foreach (KeyValuePair<PartialSolution, int> leftSol in leftTable)
                foreach (KeyValuePair<PartialSolution, int> rightSol in rightCopy)
                {
                    VertexSubset newSubset = VertexSubset.Create(bag, leftSol.Key.Subset.LocalSubset, leftSol.Key.Subset, rightSol.Key.Subset);
                    PartialSolution newSol = new PartialSolution(newSubset, leftSol.Key);

                    bool good = true;
                    foreach (int i in verticesInvolved)
                    {
                        byte rep = rightSol.Key.Find(i);
                        if (rep == i) continue;
                        if (newSol.Find(i) == newSol.Find(rep)) { good = false; break; }
                        newSol.Union(i, rep);
                    }

                    if (good)
                        Upsert(newTable, newSol, leftSol.Value + rightSol.Value);
                }

            // Do not call reduce here: the forget, introduce or join bag that comes next will take care of it
            //newTable = RankBased.Reduce(newTable, 0.5);

            return newTable;
        }

        public Dictionary<int, Dictionary<PartialSolution, int>> Join(TDNode bag, Dictionary<int, Dictionary<PartialSolution, int>> left, Dictionary<int, Dictionary<PartialSolution, int>> right)
        {
            joinSW.Start();

            Dictionary<int, Dictionary<PartialSolution, int>> result = new Dictionary<int, Dictionary<PartialSolution, int>>();

            foreach(KeyValuePair<int, Dictionary<PartialSolution, int>> kvp in left)
            {
                Dictionary<PartialSolution, int> leftTable = kvp.Value;
                Dictionary<PartialSolution, int> rightTable = null;
                if (!right.TryGetValue(kvp.Key, out rightTable) || !leftTable.Any() || !rightTable.Any())
                    continue;

                List<int> verticesInvolved = new List<int>();
                foreach (Vertex v in bag.Bag)
                    if ((leftTable.First().Key.Subset.LocalSubset & (1 << v.Color)) != 0)
                        verticesInvolved.Add(v.Color);

                Dictionary<PartialSolution, int> newTable;

                newTable = JoinTwo(bag, verticesInvolved, leftTable, rightTable);
                //newTable = JoinTwoIncremental(bag, verticesInvolved, leftTable, rightTable);

                if (newTable.Count > 0)
                    result[kvp.Key] = newTable;
            }

            VertexSubset.ClearLookup();

            joinSW.Stop();

            Program.TableCount += result.Values.Sum((t) => t.Count);

            return result;
        }

        private void Upsert(Dictionary<PartialSolution, int> table, PartialSolution s, int cost)
        {
            int oldCost = int.MaxValue;
            if (table.TryGetValue(s, out oldCost))
            {
                if (oldCost <= cost)
                {
                    return;
                }
                else
                {
                    table.Remove(s); // Needs to be removed because s might represent a different solution
                }
            }
            table[s] = cost;
        }

        // Different strategy for JoinTwo
        // The column obtained by joining two partial solutions is the bitwise AND of the columns corresponding to both solutions
        // Exploits this to attempt to speed up RBA
        public Dictionary<PartialSolution, int> JoinTwoIncremental(TDNode bag, List<int> verticesInvolved, Dictionary<PartialSolution, int> leftTable, Dictionary<PartialSolution, int> rightTable)
        {
            Dictionary<PartialSolution, int> newTable = new Dictionary<PartialSolution, int>();

            RankBased irbL = new RankBased(leftTable.First().Key.Subset);
            RankBased irbR = new RankBased(rightTable.First().Key.Subset);

            Tuple<KeyValuePair<PartialSolution, int>, Vector<uint>[]>[] leftSolutions = leftTable.OrderBy((kvp2) => kvp2.Value).Select((s) => Tuple.Create(s, irbL.GetColumn(s.Key))).ToArray();
            Tuple<KeyValuePair<PartialSolution, int>, Vector<uint>[]>[] rightSolutions = rightTable.OrderBy((kvp2) => kvp2.Value).Select((s) => Tuple.Create(s, irbR.GetColumn(s.Key))).ToArray();

            if (leftSolutions.Length > (1 << (verticesInvolved.Count - 1)))
                for (int i = 0; i < leftSolutions.Length; i++)
                    if (!irbL.AddSolution(leftSolutions[i].Item1.Key, leftSolutions[i].Item2))
                        leftSolutions[i] = null;
            if (rightSolutions.Length > (1 << (verticesInvolved.Count - 1)))
                for (int i = 0; i < rightSolutions.Length; i++)
                    if (!irbR.AddSolution(rightSolutions[i].Item1.Key, rightSolutions[i].Item2))
                        rightSolutions[i] = null;

            leftSolutions = leftSolutions.Where((s) => s != null).ToArray();
            rightSolutions = rightSolutions.Where((s) => s != null).ToArray();

            Tuple<PartialSolution, PartialSolution, Vector<uint>[], Vector<uint>[], int>[] joinedSolutions = new Tuple<PartialSolution, PartialSolution, Vector<uint>[], Vector<uint>[], int>[leftSolutions.Length * rightSolutions.Length];
            int pos = 0;
            foreach (Tuple<KeyValuePair<PartialSolution, int>, Vector<uint>[]> ls in leftSolutions)
                foreach (Tuple<KeyValuePair<PartialSolution, int>, Vector<uint>[]> rs in rightSolutions)
                    joinedSolutions[pos++] = Tuple.Create(ls.Item1.Key, rs.Item1.Key, ls.Item2, rs.Item2, ls.Item1.Value + rs.Item1.Value);

            joinedSolutions = joinedSolutions.OrderBy((s) => s.Item5).ToArray();

            //leftTable = RankBasedReduce(leftTable, 1);
            //rightTable = RankBasedReduce(rightTable, 1);

            RankBased irbJoined = new RankBased(leftTable.First().Key.Subset);

            HashSet<PartialSolution> psSeen = new HashSet<PartialSolution>();

            foreach (Tuple<PartialSolution, PartialSolution, Vector<uint>[], Vector<uint>[], int> pair in joinedSolutions)
            {
                VertexSubset newSubset = VertexSubset.Create(bag, pair.Item1.Subset.LocalSubset, pair.Item1.Subset, pair.Item2.Subset);
                PartialSolution newSol = new PartialSolution(newSubset, pair.Item1);

                bool good = true;
                foreach (int i in verticesInvolved)
                {
                    byte rep = pair.Item2.Find(i);
                    if (rep == i) continue;
                    if (newSol.Find(i) == newSol.Find(rep)) { good = false; break; }
                    newSol.Union(i, rep);
                }

                if (!good || !psSeen.Add(newSol)) continue;

                Vector<uint>[] joinedColumn = RankBased.GetArray();
                RankBased.AND(pair.Item3, pair.Item4, joinedColumn, irbJoined.rowCountVector);
                if (!irbJoined.AddSolution(newSol, joinedColumn))
                {
                    RankBased.ReleaseArray(joinedColumn);
                    continue;
                }

                newTable.Add(newSol, pair.Item5);
            }

            RankBased.ClearPool();

            return newTable;
        }
    }
}
