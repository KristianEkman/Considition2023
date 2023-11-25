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

        public static void Enrich(this MapData mapData, GeneralData generalData)
        {
            var k = 0;
            foreach (var loc in mapData.locations)
                loc.Value.IndexKey = k++;
            foreach (var hs in mapData.Hotspots)
                hs.IndexKey = k++;

            foreach (var location in mapData.locations)
            {
                foreach (var sibling in mapData.locations)
                {
                    if (sibling.Value == location.Value)
                        continue;
                    var dist = Distance(location.Value.Latitude, location.Value.Longitude, sibling.Value.Latitude, sibling.Value.Longitude);
                    if (dist < generalData.WillingnessToTravelInMeters)
                    {
                        location.Value.Siblings.Add(sibling.Value.IndexKey);
                    }
                }
            }
        }

        private static int Distance(double latitude1, double longitude1, double latitude2, double longitude2)
        {
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

            return distance;
        }
    }
}
