using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal static class DistanceCache
    {
        static int size = 5000;
        public static int[][] Values { get; set; } = new int[size][];

        internal static void Reset()
        {
            for (int i = 0; i < Values.Length; i++)
                Values[i] = new int[size];
        }
    }
}
