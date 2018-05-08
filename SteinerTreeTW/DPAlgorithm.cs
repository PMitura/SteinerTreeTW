using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    interface IDPAlgorithm<TDPTable>
    {
        TDPTable Leaf(int width);
        TDPTable Introduce(TDNode bag, Vertex[] vertices, TDPTable table);
        TDPTable Forget(TDNode bag, Vertex[] vertices, TDPTable table);
        TDPTable Join(TDNode bag, TDPTable left, TDPTable right);
        TDPTable IntroduceEdges(TDPTable table, List<Edge> edges);
    }
}
