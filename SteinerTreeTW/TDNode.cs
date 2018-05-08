using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace SteinerTreeTW
{
    class TDNode
    {
        public TreeDecomposition ParentDecomposition;
        public Vertex[] Bag; // Subset of at most (tw+1) vertices of ParentDecomposition.ParentGraph
        public List<TDNode> Adj = new List<TDNode>(); // Adjacent TDNodes
        public Vertex[] VertexByColor; // Invariant: for all v\in Bag, VertexByColor[v.Color]=v, if VertexByColor[...] is null there is no such vertex in Bag
        public byte[] VertexIndex; // Invariant: Bag[VertexIndex[v.Color]] = v

        public HashSet<Edge> availableEdges = new HashSet<Edge>();
        public List<Edge> introduceEdges = new List<Edge>();

        // Used internally for optimizing the TD
        private HashSet<Vertex> Subtree; // Stores for each vertex in the neighbourhood of the vertices in Bag whether that vertex is present in the subtree rooted here
        private Vertex[] OptimizedBag, OptimizedByColor;
        public TDNode(int n, TreeDecomposition ParentDecomposition, int w)
        {
            this.ParentDecomposition = ParentDecomposition;
            Bag = new Vertex[n];
            VertexByColor = new Vertex[w];
        }

        public T Compute<T>(IDPAlgorithm<T> algorithm, TDNode parent)
        {
            VertexSubset.ClearLookup();

            // Note: through preprocessing, certain subtrees might have become empty. Do not consider.
            IEnumerable<TDNode> children = Adj.Where((n) => n != parent && n.Subtree.Any());

            if (children.Count() == 0)
                return algorithm.IntroduceEdges(algorithm.Introduce(this, Bag, algorithm.Leaf(ParentDecomposition.Width)), introduceEdges);

            if (children.Count() == 1)
            {
                TDNode child = children.First();

                Vertex[] toForget = child.Bag.Where((v) => !this.BagContains(v)).OrderBy((v) => v.Adj.Where((w) => BagContains(w.To)).Count()).Reverse().ToArray();
                Vertex[] toIntroduce = this.Bag.Where((v) => !child.BagContains(v)).ToArray();

                if (toIntroduce.Length > 0)
                    return algorithm.IntroduceEdges(algorithm.Introduce(this, toIntroduce, child.Compute(algorithm, this)), introduceEdges);

                if (toForget.Length > 0)
                    return algorithm.IntroduceEdges(algorithm.Forget(this, toForget, child.Compute(algorithm, this)), introduceEdges);

                // No change?
                return algorithm.IntroduceEdges(child.Compute(algorithm, this), introduceEdges);
            }

            if (children.Count() > 2) throw new Exception("TD not preprocessed!");

            TDNode left = children.ElementAt(0), right = children.ElementAt(1);

            return algorithm.IntroduceEdges(algorithm.Join(this, IntroduceIfNecessary(algorithm, left, left.Compute(algorithm, this)), IntroduceIfNecessary(algorithm, right, right.Compute(algorithm, this))), introduceEdges);
        }

        public T IntroduceIfNecessary<T>(IDPAlgorithm<T> algorithm, TDNode child, T table)
        {
            Vertex[] toIntroduce = Bag.Where((v) => !child.BagContains(v)).ToArray();

            if (toIntroduce.Length > 0)
                return algorithm.Introduce(this, toIntroduce, table);

            return table;
        }
        public void FillSubtree(TDNode parent)
        {
            Subtree = new HashSet<Vertex>();

            foreach (TDNode n in Adj)
                if (n != parent)
                {
                    n.FillSubtree(this);
                    foreach (Vertex v in Bag)
                        foreach (Edge e in v.Adj)
                            if (n.Subtree.Contains(e.To))
                                Subtree.Add(e.To);
                }

            foreach (Vertex v in Bag)
                Subtree.Add(v);
        }

        public void IntroduceLate(TDNode parent)
        {
            foreach (TDNode n in Adj)
                if (n != parent)
                    n.IntroduceLate(this);

            for (int i = 0; i < OptimizedBag.Length; i++)
            {
                Vertex v = OptimizedBag[i];

                // Check if v is introduced
                if (Adj.Any((n) => n != parent && n.BagContains(v)))
                    continue;

                // Must introduce before forgetting
                if (!parent.BagContains(v))
                    continue;

                // v is not introduced, check if we can delay
                if (v.Adj.Any((e) =>
                {
                    Vertex w = e.To;
                    return BagContains(w) && !parent.BagContains(w);
                })) continue;

                // Delay introduction, remove v from bag
                OptimizedBag[i] = null;
                OptimizedByColor[v.Color] = null;
            }

            OptimizedBag = OptimizedBag.Where((v) => v != null).ToArray();
        }
        public void ForgetEarly(TDNode parent)
        {
            foreach (TDNode n in Adj)
                if (n != parent)
                    n.ForgetEarly(this);

            // If we have already processed all neighbours of a given vertex, we can forget it
            foreach (Vertex v in OptimizedBag)
                if (!v.Adj.Any((e) => !Subtree.Contains(e.To)))
                    parent.RemoveVertex(v, this);
        }

        // Removes a vertex from a subtree 
        public void RemoveVertex(Vertex v, TDNode parent)
        {
            if (OptimizedByColor[v.Color] != v)
                return;

            OptimizedBag = OptimizedBag.Where((u) => u != v).ToArray();
            OptimizedByColor[v.Color] = null;

            foreach (TDNode n in Adj)
                if (n != parent)
                    n.RemoveVertex(v, this);
        }
        public double CalculateCost()
        {
            // Initialize subtrees
            FillSubtree(null);

            // Copy bag/color arrays to original state
            foreach (TDNode n in ParentDecomposition.Nodes)
            {
                n.OptimizedBag = (Vertex[])n.Bag.Clone();
                n.OptimizedByColor = (Vertex[])n.VertexByColor.Clone();
            }

            // Optimize the decomposition for early forgets/introduces
            /*foreach (TDNode n in Adj)
                n.ForgetEarly(this);

            foreach (TDNode n in Adj)
                n.IntroduceLate(this);*/

            return CalculateCost(null);
        }

        public double CalculateCost(TDNode parent)
        {
            IEnumerable<TDNode> children = Adj.Where((n) => n != parent);

            if (children.Count() == 1)
                return children.First().CalculateCost(this) + Math.Pow(4, Math.Max(OptimizedBag.Length, children.First().OptimizedBag.Length) - ParentDecomposition.Width);

            if (children.Count() == 0)
                return 0;

            // Guesstimate how much a join will cost
            double result = 0;

            foreach (TDNode child in children)
                result += child.CalculateCost(this);

            Queue<int> q = new Queue<int>(children.Select((c) => c.OptimizedBag.Length).OrderBy((x) => x));
            while (q.Count > 2)
            {
                int a = q.Dequeue(), b = q.Dequeue();
                q.Enqueue(Math.Min(Math.Max(a, b) + 1, OptimizedBag.Length));
                result += Math.Pow(5, Math.Max(a, b) + 1 - ParentDecomposition.Width);
            }

            return result;
        }

        // Splits up joins into binary joins in a heuristically efficient way
        public void DeconstructJoins(TDNode parent)
        {
            IEnumerable<TDNode> children = Adj.Where((n) => n != parent);

            foreach (TDNode child in children)
                child.DeconstructJoins(this);

            if (children.Count() <= 2)
                return;

            List<Tuple<int, TDNode>> toJoin = new List<Tuple<int, TDNode>>();
            foreach (TDNode child in children)
            {
                int code = 0;
                foreach (Vertex v in child.Bag)
                    code |= 1 << v.Color;
                toJoin.Add(Tuple.Create(code, child));
                child.Adj.Remove(this);
            }

            this.Adj.Clear();
            Adj.Add(parent);

            while (toJoin.Count > 2 || (toJoin.Count == 2 && (toJoin[0].Item1 | toJoin[1].Item1).BitCount() < Bag.Length))
            {
                int min = int.MaxValue; Tuple<int, TDNode> left = null, right = null;
                for (int i = 0; i < toJoin.Count; i++)
                    for (int j = i + 1; j < toJoin.Count; j++)
                        if ((toJoin[i].Item1 | toJoin[j].Item1).BitCount() < min)
                        {
                            min = (toJoin[i].Item1 | toJoin[j].Item1).BitCount();
                            left = toJoin[i]; right = toJoin[j];
                        }

                toJoin.Remove(left); toJoin.Remove(right);

                TDNode newNode = new TDNode((left.Item1 | right.Item1).BitCount(), ParentDecomposition, VertexByColor.Length);
                toJoin.Add(Tuple.Create(left.Item1 | right.Item1, newNode));

                newNode.Adj.Add(left.Item2);
                newNode.Adj.Add(right.Item2);
                left.Item2.Adj.Add(newNode);
                right.Item2.Adj.Add(newNode);

                ParentDecomposition.Nodes.Add(newNode);

                int c = 0;
                for (int i = 0; i < VertexByColor.Length; i++)
                {
                    if (left.Item2.VertexByColor[i] != null)
                    {
                        newNode.Bag[c++] = left.Item2.VertexByColor[i];
                        newNode.VertexByColor[i] = left.Item2.VertexByColor[i];
                    }
                    else if (right.Item2.VertexByColor[i] != null)
                    {
                        newNode.Bag[c++] = right.Item2.VertexByColor[i];
                        newNode.VertexByColor[i] = right.Item2.VertexByColor[i];
                    }
                }
            }

            this.Adj.Add(toJoin[0].Item2);
            toJoin[0].Item2.Adj.Add(this);

            if (toJoin.Count > 1)
            {
                this.Adj.Add(toJoin[1].Item2);
                toJoin[1].Item2.Adj.Add(this);
            }
        }

        // Adds a forget bag before any introduce
        public void ForgetBeforeIntroduce(TDNode parent)
        {
            IEnumerable<TDNode> children = Adj.Where((n) => n != parent);

            if (children.Count() == 1)
            {
                TDNode child = children.First();

                IEnumerable<Vertex> toForget = child.Bag.Where((v) => !this.BagContains(v));
                IEnumerable<Vertex> toIntroduce = this.Bag.Where((v) => !child.BagContains(v));

                if (toForget.Any() && toIntroduce.Any())
                {
                    TDNode newNode = new TDNode(0, ParentDecomposition, VertexByColor.Length);
                    newNode.Bag = child.Bag.Where((v) => this.BagContains(v)).ToArray();
                    foreach (Vertex v in newNode.Bag)
                        newNode.VertexByColor[v.Color] = v;

                    this.Adj.Remove(child);
                    this.Adj.Add(newNode);

                    child.Adj.Remove(this);
                    child.Adj.Add(newNode);

                    newNode.Adj.Add(child);
                    newNode.Adj.Add(this);

                    ParentDecomposition.Nodes.Add(newNode);

                    newNode.ForgetBeforeIntroduce(this);
                    return;
                }

                child.ForgetBeforeIntroduce(this);
                return;
            }

            foreach (TDNode child in children)
                child.ForgetBeforeIntroduce(this);
        }

        // Adds extra (forget) bags so that the children of each join are a proper subset of the join root bag
        public void PreJoinForget(TDNode parent)
        {
            IEnumerable<TDNode> children = Adj.Where((n) => n != parent);

            if (children.Count() == 1)
            {
                children.First().PreJoinForget(this);
                return;
            }

            if (children.Count() == 0)
                return;

            for (int i = 0; i < Adj.Count; i++)
            {
                if (Adj[i] == parent) continue;

                if (Adj[i].Bag.Any((v) => !this.BagContains(v)))
                {
                    TDNode newNode = new TDNode(0, ParentDecomposition, VertexByColor.Length);
                    newNode.Bag = Adj[i].Bag.Where((v) => this.BagContains(v)).ToArray();
                    foreach (Vertex v in newNode.Bag)
                        newNode.VertexByColor[v.Color] = v;

                    newNode.Adj.Add(this);
                    newNode.Adj.Add(Adj[i]);
                    Adj[i].Adj[Adj[i].Adj.IndexOf(this)] = newNode;
                    Adj[i] = newNode;

                    ParentDecomposition.Nodes.Add(newNode);
                }

                Adj[i].PreJoinForget(this);
            }
        }
        public void ColorVertices()
        {
            for (int i = 0; i < Bag.Length; i++)
            {
                VertexByColor[i] = Bag[i];
                Bag[i].Color = i;
            }

            foreach (TDNode nb in Adj)
                nb.ColorVertices(this);
        }

        // Computes a (tw+1)-coloring
        public void ColorVertices(TDNode parent)
        {
            for (int i = 0; i < Bag.Length; i++)
            {
                if (Bag[i].Color >= 0)
                    VertexByColor[Bag[i].Color] = Bag[i];
            }

            int firstColor = 0;
            for (int i = 0; i < Bag.Length; i++)
            {
                if (Bag[i].Color < 0)
                {
                    while (VertexByColor[firstColor] != null)
                        firstColor++;
                    VertexByColor[firstColor] = Bag[i];
                    Bag[i].Color = firstColor;
                }
            }

            foreach (TDNode nb in Adj)
                if (nb != parent)
                    nb.ColorVertices(this);
        }

        // Maps colors back to positions in the Bag-array
        public void IndexVertices(TDNode parent)
        {
            foreach (TDNode n in Adj)
                if (n != parent)
                    n.IndexVertices(this);

            Bag = Bag.OrderBy((v) => v.Color).ToArray();

            VertexIndex = new byte[VertexByColor.Length];

            for (int i = 0; i < Bag.Length; i++)
                VertexIndex[Bag[i].Color] = (byte)i;
        }

        public void SetBagsFinal()
        {
            Bag = OptimizedBag;
            VertexByColor = OptimizedByColor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool BagContains(Vertex v)
        {
            return VertexByColor[v.Color] == v;
        }
    }
}
