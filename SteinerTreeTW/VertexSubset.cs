using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    class VertexSubset
    {
        public TDNode ParentBag;
        public int LocalSubset;
        public VertexSubset Left, Right;

        /* WARNING
         *  Equality (and GetHashCode()) for VertexSubset are defined in a somewhat strange way
         *  Two VertexSubsets are considered equal if they have the same LocalSubset value and
         *  the VertexSubset Left and Right **references** are equal (!)
         *  This means that two VertexSubset objects that represent the same subset might be considered unequal
         *  If they are built up in a different way using different references
         *  The idea behind this is to allow fast equality comparison while still getting a good approximation of "equals"
         *  This behavior can be used in the Create method, but it is currently not using it
        */

        public override int GetHashCode()
        {
            int code = 0;
            if (Left != null) code ^= Left.OriginalHashCode();
            if (Right != null) code ^= Right.OriginalHashCode();
            if (ParentBag != null) code ^= ParentBag.GetHashCode();
            code = ((code << 5) + code) ^ LocalSubset;
            return code;
        }

        public int OriginalHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            VertexSubset objSet = obj as VertexSubset;

            if (objSet == null)
                return false;

            return objSet.ParentBag == ParentBag && objSet.LocalSubset == LocalSubset && Object.ReferenceEquals(objSet.Left, Left) && Object.ReferenceEquals(objSet.Right, Right);
        }

        // Allows sharing of references, saves memory but costs time so disabled for now
        static Dictionary<VertexSubset, VertexSubset> Lookup = new Dictionary<VertexSubset, VertexSubset>();
        public static VertexSubset Create(TDNode ParentBag, int LocalSubset, VertexSubset Left, VertexSubset Right)
        {
            VertexSubset result = new VertexSubset() { ParentBag = ParentBag, LocalSubset = LocalSubset, Left = Left, Right = Right };
            return result;

            VertexSubset preExisting = null;
            if (Lookup.TryGetValue(result, out preExisting))
                return preExisting;
            Lookup[result] = result;
            return result;
        }

        public static void ClearLookup()
        {
            Lookup.Clear();
        }
    }
}
