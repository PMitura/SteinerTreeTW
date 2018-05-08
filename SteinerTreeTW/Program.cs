using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace SteinerTreeTW
{
    class Program
    {
        public const bool Debug = false;
        
        public static Random r = new Random(4321);
        static Graph g;
        static TreeDecomposition decomposition;

        public static long XORCount, TableCount, GenerateCount;
        static void Main(string[] args)
        {
            int seed = 4321;

            // Prescribed method of initializing randomness
            if(Array.IndexOf(args, "-s") < args.Length - 1)
            {
                try
                {
                    seed = int.Parse(args[Array.IndexOf(args, "-s") + 1]);
                }
                catch { }
            }

            Stopwatch sw = new Stopwatch();

            //StreamWriter outSW = new StreamWriter("output.txt");
            //Console.SetOut(outSW);

            //for (int i = 79; i < 200; i += 2)
            {
                r = new Random(seed);

                //outSW.Flush();
                //if (i == 79) continue; // This testcase is very large and slow
                sw.Restart();
                
                //if (Debug) Console.Write(i.ToString().PadLeft(3, '0') + " ");
                StreamReader sr = null;
                //sr = new StreamReader(String.Format("../../../../../instances/instance{0}.gr", i.ToString().PadLeft(3, '0')));

                g = Graph.Parse(sr ?? Console.In);
                decomposition = TreeDecomposition.Parse(sr ?? Console.In, g);

                InitUF(g);

                List<Edge> forcedEdges = new List<Edge>();

                // Find edges that are forced because they are between two terminals and there is no path with lighter bottleneck
                foreach (Edge e in g.Edges.OrderBy((e) => e.Weight).ThenBy((e) => e.To.IsTerminal && e.From.IsTerminal ? 0 : 1))
                {
                    if (Find(e.From.Id) == Find(e.To.Id)) continue;
                    if (e.To.IsTerminal && e.From.IsTerminal)
                        forcedEdges.Add(e);
                    Union(e.From.Id, e.To.Id);
                }
                
                if (Debug) Console.WriteLine("Forced: " + forcedEdges.Count);

                // Use Dreyfus-Wagner or Erickson-Monma-Veinott if FPT (in nubmer of terminals) is faster than treewidth DP
                if (Math.Pow(5.0, decomposition.Width - Math.Log(1000000, 5.0)) > Math.Pow(3.0, g.Vertices.Where((v) => v.IsTerminal).Count() - forcedEdges.Count - Math.Log(1000000, 3.0)))
                {
                    foreach (Edge e in forcedEdges)
                    {
                        if (e.From.IsTerminal)
                            e.From.IsTerminal = false;
                        else
                            e.To.IsTerminal = false;
                        e.From.Adj.Add(new Edge(e.From, e.To, 0));
                        e.To.Adj.Add(new Edge(e.To, e.From, 0));
                    }

                    //new DreyfusWagner(g, forcedEdges).Solve();
                    new EricksonMonmaVeinott(g, forcedEdges).Solve();

                    if (Debug)
                    {
                        Console.WriteLine(sw.ElapsedMilliseconds);
                        //continue; // Should be commented out when Debug is false
                    }

                    return;
                }

                XORCount = 0; TableCount = 0; GenerateCount = 0;

                // Actually run the DP algorithm
                decomposition.FindOptimalRoot();
                SteinerAlgorithm algo = new SteinerAlgorithm();
                Dictionary<int, Dictionary<PartialSolution, int>> result = decomposition.Compute(algo);
                if(Debug) algo.Diagnostics();

                // Find best solution
                int bestVal = int.MaxValue; PartialSolution bestSol = new PartialSolution();
                foreach (Dictionary<PartialSolution, int> table in result.Values)
                {
                    foreach (KeyValuePair<PartialSolution, int> kvp in table)
                    {
                        if (kvp.Value >= bestVal || kvp.Key.CountComponents() > 1) continue;

                        bestVal = kvp.Value;
                        bestSol = kvp.Key;
                    }
                }
                
                if (bestVal == int.MaxValue)
                {
                    Console.WriteLine("Impossible");
                }
                else
                {
                    if (Debug) Console.Write(Math.Round(sw.ElapsedMilliseconds / 1000.0, 1) + "s\tSolutions: {0}\tRows Generated: {1}\tXORCount: {2}\t", TableCount, GenerateCount, XORCount);

                    Console.WriteLine("VALUE " + bestVal);

                    if (Debug) Console.WriteLine();

                    // Reconstruct subset of vertices
                    HashSet<int> solution = new HashSet<int>();
                    Stack<VertexSubset> subsets = new Stack<VertexSubset>();
                    subsets.Push(bestSol.Subset);
                    while (subsets.Count > 0)
                    {
                        VertexSubset subset = subsets.Pop();
                        if (subset == null || subset.ParentBag == null) continue;
                        subsets.Push(subset.Left); subsets.Push(subset.Right);

                        foreach (Vertex v in subset.ParentBag.Bag)
                            if ((subset.LocalSubset & (1 << v.Color)) != 0)
                                solution.Add(v.Id);
                    }

                    InitUF(g);

                    // Compute MST on vertex set
                    int total = 0; int count = 0;
                    foreach (Edge e in g.Edges.OrderBy((e) => e.Weight).ThenBy((e) => Math.Min(e.To.Id, e.From.Id)).ThenBy((e) => Math.Max(e.To.Id, e.From.Id)))
                    {
                        if (!solution.Contains(e.To.Id) || !solution.Contains(e.From.Id))
                            continue;
                        if (Find(e.To.Id) == Find(e.From.Id))
                            continue;
                        Union(e.To.Id, e.From.Id);
                        if (!Debug) Console.WriteLine(Math.Min((e.From.Id + 1), (e.To.Id + 1)) + " " + Math.Max((e.From.Id + 1), (e.To.Id + 1)));
                        total += e.Weight;
                        count++;
                    }
                }
            }

            if(Debug) Console.ReadLine();
        }

        static int[] UnionFind;
        static int Find(int elem)
        {
            int x = elem;

            while (UnionFind[x] != x)
            {
                int next = UnionFind[x];
                UnionFind[x] = UnionFind[next];
                x = next;
            }

            return x;
        }

        static void InitUF(Graph g)
        {
            UnionFind = new int[g.Vertices.Length];
            for (int j = 0; j < UnionFind.Length; j++)
                UnionFind[j] = j;
        }

        static void Union(int elem1, int elem2)
        {
            int x = Find(elem1), y = Find(elem2);
            UnionFind[y] = x;
        }
    }
}
