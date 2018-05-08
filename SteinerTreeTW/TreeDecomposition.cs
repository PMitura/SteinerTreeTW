using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SteinerTreeTW
{
    class TreeDecomposition
    {
        public List<TDNode> Nodes;
        public int Width;
        public Graph ParentGraph;

        public double EstimatedCost = double.MaxValue;
        public TDNode Root;

        public TreeDecomposition(int n, int w)
        {
            if (w > 32) throw new ArgumentException("Only graphs up to treewidth 31 (bag size 32) are supported.");

            Nodes = new List<TDNode>(n);
            for (int i = 0; i < n; i++) Nodes.Add(null);
            Width = w;
        }

        public T Compute<T>(IDPAlgorithm<T> algorithm)
        {
            return Root.Compute(algorithm, null);
        }

        // Sets the node to pick as the root that results in the lowest cost. Randomly tries a small number of candidates if the decomposition is large.
        public void FindOptimalRoot()
        {
            List<TDNode> todo = new List<TDNode>(Nodes.Where((n) => n.Bag.Any((v) => v.IsTerminal)));

            int t = 0;

            while(t < 1000000 && todo.Count > 0)
            {
                int p = Program.r.Next(todo.Count);
                TDNode n = todo[p];
                todo[p] = todo[todo.Count - 1];
                todo.RemoveAt(todo.Count - 1);

                double cost = n.CalculateCost();
                
                if(cost < EstimatedCost)
                {
                    EstimatedCost = cost;
                    Root = n;
                }

                t += Nodes.Count;
            }

            Root.CalculateCost();

            foreach (TDNode n in Nodes)
                n.SetBagsFinal();

            Root.PreJoinForget(null);

            Root.DeconstructJoins(null);

            Root.ForgetBeforeIntroduce(null);

            Root.IndexVertices(null);

            Root.FillSubtree(null);

            SetupEdges();
        }

        // Assigns edges to bags to be introduced
        public void SetupEdges()
        {
            foreach(TDNode n in Nodes)
            {
                n.availableEdges = new HashSet<Edge>();
                foreach (Vertex v in n.Bag)
                    foreach (Edge e in v.Adj)
                        if(n.BagContains(e.To))
                            n.availableEdges.Add(e);
            }

            while (true)
            {
                IEnumerable<TDNode> nonEmpty = Nodes.Where((n) => n.availableEdges.Count > 0);
                if (!nonEmpty.Any()) break;

                // Heuristically/greedily find a set cover
                int maxVal = nonEmpty.Min((n) => n.availableEdges.Count);
                TDNode maxNode = nonEmpty.Where((n) => n.availableEdges.Count == maxVal).First();

                maxNode.introduceEdges = maxNode.availableEdges.ToList();

                foreach(Edge e in maxNode.introduceEdges)
                    foreach (TDNode n in Nodes)
                        n.availableEdges.Remove(e);
            }
        }

        // Parses a tree decomposition for a graph g, reading the input from a specific streamreader sr
        public static TreeDecomposition Parse(TextReader sr, Graph g)
        {
            TreeDecomposition td = null;

            for (string line = sr.ReadLine(); line != "END"; line = sr.ReadLine())
            {
                string[] cf = line.Split();
                if (cf[0] == "s")
                {
                    td = new TreeDecomposition(int.Parse(cf[2]), int.Parse(cf[3]));
                }
                else if (cf[0] == "b")
                {
                    TDNode newNode = new TDNode(cf.Length - 2, td, td.Width);
                    for (int i = 2; i < cf.Length; i++)
                        newNode.Bag[i - 2] = g.Vertices[int.Parse(cf[i]) - 1];
                    td.Nodes[int.Parse(cf[1]) - 1] = newNode;
                }
                else
                {
                    try
                    {
                        int a = int.Parse(cf[0]);
                        int b = int.Parse(cf[1]);
                        td.Nodes[a - 1].Adj.Add(td.Nodes[b - 1]);
                        td.Nodes[b - 1].Adj.Add(td.Nodes[a - 1]);
                    }
                    catch
                    { }
                }
            }

            td.ParentGraph = g;

            td.Nodes[0].ColorVertices();

            if (Program.Debug) Console.WriteLine("Bags: {0} - Join Bags: {1} - Width: {2} - Vertices: {3} - Terminals: {4}", td.Nodes.Count, td.Nodes.Where((n) => n.Adj.Count > 2).Count(), td.Width, g.Vertices.Length, g.Vertices.Where((v) => v.IsTerminal).Count());

            return td;
        }

        public void PruneRedundantBags()
        {
            Stack<TDNode> leaves = new Stack<TDNode>();
            foreach (TDNode n in Nodes)
                if (n.Adj.Count < 1)
                    leaves.Push(n);
            while(leaves.Count > 0)
            {
                TDNode leaf = leaves.Pop();
                if(leaf.Adj[0].Bag.OrderBy((v) => v.Color).SequenceEqual(leaf.Bag.OrderBy((v) => v.Color)))
                {
                    leaf.Bag = null;
                    leaf.Adj[0].Adj.Remove(leaf);
                    if (leaf.Adj[0].Adj.Count == 1)
                        leaves.Push(leaf.Adj[0]);
                }
            }
            int a = Nodes.Count;
            Nodes = Nodes.Where((n) => n.Bag != null).ToList();
            if(a != Nodes.Count) Console.WriteLine(a + " " + Nodes.Count);
        }
    }
}
