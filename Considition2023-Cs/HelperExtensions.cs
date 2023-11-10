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

        public static string ToSI(this double d, string format = "0.###")
        {
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

        public static int DistanceTo(this StoreLocationScoring location1, StoreLocationScoring location2)
        {
            if (Scoring.Distances[location1.IndexKey][location2.IndexKey] != 0)
                return Scoring.Distances[location1.IndexKey][location2.IndexKey];
            if (Scoring.Distances[location2.IndexKey][location1.IndexKey] != 0)
                return Scoring.Distances[location2.IndexKey][location1.IndexKey];

            double latitude1 = location1.Latitude;
            double longitude1 = location1.Longitude;
            double latitude2 = location2.Latitude;
            double longitude2 = location2.Longitude;
            double r = 6371e3;
            double latRadian1 = latitude1 * Math.PI / 180;
            double latRadian2 = latitude2 * Math.PI / 180;

            double latDelta = (latitude2 - latitude1) * Math.PI / 180;
            double longDelta = (longitude2 - longitude1) * Math.PI / 180;

            double a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                Math.Cos(latRadian1) * Math.Cos(latRadian2) *
                Math.Sin(longDelta / 2) * Math.Sin(longDelta / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            int distance = (int)Math.Round(r * c, 0);

            Scoring.Distances[location1.IndexKey][location2.IndexKey] = distance;
            Scoring.Distances[location2.IndexKey][location1.IndexKey] = distance;

            return distance;
        }

        public static string Apikey = "20d51f18-3a6f-4419-8466-1fac81f7e540";


    }
}
