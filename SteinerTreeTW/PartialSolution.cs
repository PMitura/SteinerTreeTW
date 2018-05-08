using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    struct PartialSolution
    {
        public VertexSubset Subset;
        public byte[] UnionFind;

        int hashCode;
        public PartialSolution(int width)
        {
            Subset = null;
            UnionFind = new byte[width];
            for (byte i = 0; i < UnionFind.Length; i++)
                UnionFind[i] = i;
            hashCode = 0;
        }
        public PartialSolution(VertexSubset subset)
        {
            Subset = subset;
            UnionFind = new byte[subset.ParentBag.ParentDecomposition.Width];
            for (byte i = 0; i < UnionFind.Length; i++)
                UnionFind[i] = i;
            hashCode = 0;
        }
        public PartialSolution(VertexSubset subset, PartialSolution parent)
        {
            Subset = subset;
            UnionFind = new byte[subset == null ? 0 : subset.ParentBag.ParentDecomposition.Width];
            for (byte i = 0; i < UnionFind.Length; i++)
                UnionFind[i] = parent.UnionFind == null ? i : parent.UnionFind[i];
            hashCode = 0;

            // Check if any vertices have been forgotten
            if (parent.Subset == null || ((subset.LocalSubset ^ parent.Subset.LocalSubset) & parent.Subset.LocalSubset) == 0)
                return;

            // Rework union-find to fix forgotten vertices
            for (byte i = 0; i < UnionFind.Length; i++)
            {
                if ((Subset.LocalSubset & (1 << i)) != 0 && (parent.Subset.LocalSubset & (1 << i)) != 0)
                {
                    byte rep = Find(i);
                    if ((Subset.LocalSubset & (1 << rep)) == 0)
                    {
                        UnionFind[rep] = i;
                        UnionFind[i] = i;
                    }
                }
            }

            for (byte i = 0; i < UnionFind.Length; i++)
                if ((Subset.LocalSubset & (1 << i)) == 0)
                    UnionFind[i] = i;
        }
        public override int GetHashCode()
        {
            if (hashCode != 0) return hashCode;

            hashCode = 5381;

            for(int i = 0; i < UnionFind.Length; i++)
            {
                hashCode = (hashCode << 5) + hashCode;
                hashCode ^= Find(i);
            }

            if(Subset != null) hashCode ^= Subset.LocalSubset.GetHashCode();

            return hashCode;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            PartialSolution objSol = (PartialSolution)obj;

            if (Subset.LocalSubset != objSol.Subset.LocalSubset) return false;

            for (int i = 0; i < UnionFind.Length; i++)
                if (Find(i) != objSol.Find(i))
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Find(int elem)
        {
            byte x = (byte)elem;

            while (UnionFind[x] != x)
            {
                byte next = UnionFind[x];
                UnionFind[x] = UnionFind[next];
                x = next;
            }

            return x;
        }

        // This method of doing unions guarantees uniqueness of the representation of a given disjoint set
        // Note that path compression still gives a O(log n)-time guarantee on operations
        public void Union(int elem1, int elem2)
        {
            byte x = Find((byte)elem1), y = Find((byte)elem2);

            if (x > y)
            {
                UnionFind[x] = y;
                hashCode = 0;
            }
            else
            {
                UnionFind[y] = x;
                hashCode = 0;
            }
        }        

        // Checks the number of connected components in the UF structure
        public int CountComponents()
        {
            return CountComponents(Subset.LocalSubset);
        }

        // Checks the number of connected components among a given subset of vertices
        public int CountComponents(int mask)
        {
            int seen = 0;
            for(int i = 0; i < UnionFind.Length; i++)
            {
                if (((1 << i) & mask) == 0) continue;
                seen |= (1 << Find(i));
            }
            return seen.BitCount();
        }
    }
}
