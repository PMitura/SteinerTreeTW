using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SteinerTreeTW
{
    class Graph
    {
        public Vertex[] Vertices;
        public List<Edge> Edges;

        public int[,] distance;
        public Edge?[,] pred;
        public Graph(int n)
        {
            Vertices = new Vertex[n];
            for (int i = 0; i < n; i++)
                Vertices[i] = new Vertex(i);                
            Edges = new List<Edge>();
        }
        public static Graph Parse(TextReader sr)
        {
            Graph G = null;

            for (string line = sr.ReadLine(); line != "END"; line = sr.ReadLine())
            {
                string[] cf = line.Split();
                if (cf[0] == "Nodes")
                    G = new Graph(int.Parse(cf[1]));
                if (cf[0] == "E" && G != null)
                    G.AddEdge(int.Parse(cf[1]) - 1, int.Parse(cf[2]) - 1, int.Parse(cf[3]));
            }

            for (string line = sr.ReadLine(); line != "END"; line = sr.ReadLine())
            {
                string[] cf = line.Split();
                if (cf[0] == "T")
                    G.Vertices[int.Parse(cf[1]) - 1].IsTerminal = true;
            }

            if (!G.Vertices.Any((v) => v.IsTerminal)) throw new Exception("There should be at least one terminal!");

            return G;
        }

        public void AddEdge(int a, int b, int w)
        {
            Vertices[a].Adj.Add(new Edge(Vertices[a], Vertices[b], w));
            Vertices[b].Adj.Add(new Edge(Vertices[b], Vertices[a], w));

            if (a < b)
                Edges.Add(new Edge(Vertices[a], Vertices[b], w));
            else
                Edges.Add(new Edge(Vertices[b], Vertices[a], w));
        }

        public int n
        {
            get
            {
                return Vertices.Length;
            }
        }

        // Compute distance Matrix using Dijkstra's algorithm
        public void ComputeDistances()
        {
            distance = new int[n, n];
            pred = new Edge?[n, n];

            PriorityQueue<Vertex> pq = new PriorityQueue<Vertex>(Edges.Count);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    distance[i, j] = int.MaxValue;
                pq.Enqueue(Vertices[i], 0);
                distance[i, i] = 0;
                bool[] visited = new bool[n];

                while (!pq.IsEmpty())
                {
                    Vertex cur = pq.Dequeue();
                    if (visited[cur.Id]) continue;
                    visited[cur.Id] = true;

                    foreach (Edge e in cur.Adj)
                    {
                        if (distance[i, cur.Id] + e.Weight < distance[i, e.To.Id])
                        {
                            distance[i, e.To.Id] = distance[i, cur.Id] + e.Weight;
                            pred[i, e.To.Id] = e;
                            pq.Enqueue(e.To, distance[i, e.To.Id]);
                        }
                    }
                }
            }
        }
    }
}
