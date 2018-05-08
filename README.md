This is a submission to the 2018 PACE challenge, track B: Computing Steiner Trees in graphs with low treewidth
Submission by: Tom C. van der Zanden (Utrecht University)

The algorithm uses "standard" dynamic programming, augmented with the rank-based approach.
Because a number of the (public) PACE instances had a very small number of terminals, we also implemented the Erickson-Monma-Veinott algorithm which is used if 3^#terminals is less than 5^treewidth.


The implementation has some interesting special tricks/features:

1) We compute a (tw+1)-coloring of the graph, and use a vertex' color number to build bitstrings to represent subsets of vertices within bags (this is also implemented in Luuk van der Graaff's master's thesis). The whole solution uses bitstrings quite extensively.

2) AVX instructions get faster row/column operations in the rank-based approach. The difference, in practice, is quite marginal.

3) The GenerateCuts method that is used to generate the cuts consistent with a given partition is quite efficient, as it computes the blocks of the partition on the fly and then (output-sensitively) enumerates only the 2^#blocks consistent cuts. In his master's thesis, Luuk van der Graaff mentions that representing solutions by explicitly storing blocks enables faster cut enumeration. We don't use this (more expensive) method of representing solutions, but instead generate the necessary blocks on the fly.

4) A special edge introduction strategy: if one introduces E edges one at a time, this requires O(E * (2^tw)^3) time in the worst case, since the rank-based approach should be ran after introducing each edge to keep the size of the tables down.
We implemented a strategy (see the IntroduceEdgesIntoTable method) that can do E edge introductions in O(E * (2^tw)^2 + (2^tw)^3) time. For the PACE instances, this does not do much, but it is possible to craft instances (with very many edges introduced at once) where this makes a significant difference.

5) The Union-Find data structure used to represent partitions is canonical: the representative of an element is always the lowest-numbered vertex in its partition class. This invariant is very easy to maintain by always taking the lower-numbered child as root when doing an union and this does not (especially considering the small size of the sets involved) affect the running time very much.

6) Solutions are represented only by their vertices and not by their edges. This is more space efficient, and a minimum spanning tree on the vertex set is computed using Kruskal's algorithm. Vertex subsets are represented as trees (in the same shape as the tree decomposition)

7) The algorithm preprocesses the tree decomposition to a semi-nice form, and tries a few random vertices as root and picks the one with lowest cost.


Our treewidth-based algorithm solves all of the public PACE instances up to and including instance 105 (except 079) well within the 30 minute time limit. Additionally, we are able to solve (on an Intel Core i7-6700 with 32GB) the instances:
 - 079 with treewidth 10 and 36415 vertices in 2.9 hours
 - 107 with treewidth 15 and 160 vertices in 7.3 hours
 - 111 with treewidth 16 and 2763 vertices in 34 minutes
Note that this is very sensitive to the exact tresholds for then the rank-based reduce is invoked. For instance, with fewer reduces instance 107 could be solved in 2.5 hours. We use thresholds (comparing the size of the table to the size it would have after reducing) to determine when the rank-based approach should be ran: just prior to a join these thresholds are low (because any excess instances may blow up to many more instances in the result).  It is also beneficial to reduce just before vertices are introduced, since the bag being one or two vertices smaller can make a very significant difference to the running time of the reduce algorithm.


Compilation/running instructions
The solution is written in C#.
 - On Windows, the solution can be opened, compiled and ran with Visual Studio.
 - On Linux/Mac, the solution can be compiled and ran with Mono. When calling mcs, specify the flag "-r:System.Numerics.Vectors.dll"