using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteinerTreeTW
{
    public struct Edge
    {
        public int Weight;
        public Vertex From, To;

        public Edge(Vertex From, Vertex To) : this(From, To, 0)
        {

        }
        public Edge(Vertex From, Vertex To, int Weight)
        {
            this.From = From; this.To = To; this.Weight = Weight;
        }

        public override bool Equals(object obj)
        {
            Edge oth = (Edge)obj;
            //return oth.Weight == this.Weight && ((oth.To == this.To && oth.From == this.From) || (oth.To == this.From && oth.From == this.To));
            return (oth.To == this.To && oth.From == this.From) || (oth.To == this.From && oth.From == this.To);
        }

        public override int GetHashCode()
        {
            if (From.Id < To.Id)
            {
                int hashCode = From.GetHashCode();
                hashCode = ((hashCode << 5) + hashCode) ^ To.GetHashCode();
                //hashCode = ((hashCode << 5) + hashCode) ^ Weight.GetHashCode();
                return hashCode;
            }
            else
            {
                int hashCode = To.GetHashCode();
                hashCode = ((hashCode << 5) + hashCode) ^ From.GetHashCode();
                //hashCode = ((hashCode << 5) + hashCode) ^ Weight.GetHashCode();
                return hashCode;
            }
        }
    }
}
