using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal static class DistanceCache
    {
        internal static int[][] Values { get; private set; }

        internal static void Reset(int size)
        {
            Values = new int[size][];
            for (int i = 0; i < Values.Length; i++)
                Values[i] = new int[size];
        }
    }
}
