﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal static class HelperExtensions
    {
        public static void PrintJson(this object obj)
        {
            Console.WriteLine(JsonConvert.SerializeObject(obj, Formatting.Indented));
        }

        public static string ToSI(this double d, string format = "0.###")
        {
            if (d == 0)
                return "0";
            char[] incPrefixes = new[] { 'k', 'M', 'G', 'T', 'P', 'E', 'Z', 'Y' };
            char[] decPrefixes = new[] { 'm', '\u03bc', 'n', 'p', 'f', 'a', 'z', 'y' };

            int degree = (int)Math.Floor(Math.Log10(Math.Abs(d)) / 3);
            double scaled = d * Math.Pow(1000, -degree);

            char? prefix = null;
            switch (Math.Sign(degree))
            {
                case 1: prefix = incPrefixes[degree - 1]; break;
                case -1: prefix = decPrefixes[-degree - 1]; break;
            }

            return scaled.ToString(format) + prefix;
        }

        public static string Apikey = "20d51f18-3a6f-4419-8466-1fac81f7e540";

        public static KeyValuePair<string, LocationType>[] LocationTypes { get; set; }

        public static void SaveTo(this string fileText, string filename)
        {
            if (!Directory.Exists("saved"))
                Directory.CreateDirectory("saved");
            File.WriteAllText(Path.Combine("saved", filename), fileText);
        }

        public static string ReadFileText(this string filename)
        {
            var path = Path.Combine("saved", filename);
            if (File.Exists(path))
                return File.ReadAllText(path);
            return string.Empty;
        }
    }
}
