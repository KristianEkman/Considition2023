using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal static class HelperExtensions
    {
        public static void PrintJson(this object obj) { 
            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }



    }
}
