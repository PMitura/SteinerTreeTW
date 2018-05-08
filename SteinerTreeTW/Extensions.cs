using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace SteinerTreeTW
{
    public static class Extensions
    {
        // Black magic from https://stackoverflow.com/a/109025/1327791
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitCount(this int i)
        {
            i = i - ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
        }
    }
}