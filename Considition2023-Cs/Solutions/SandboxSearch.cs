using System.Text.Json.Nodes;
using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using Newtonsoft.Json;

public class SandboxSearch
{
    static Random Rnd = new(777);
    const int MaxStations = 2;
    internal static int ChildCount = 500;
    internal static int Mutations = 3;

    static double LongitudeMax = 0;
    static double LongitudeMin = 0;
    static double LatitudeMax = 0;
    static double LatitudeMin = 0;
    struct ChildItem
    {
        public int F3100Count { get; set; }
        public int F9100Count { get; set; }
        public int LocationType { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
    }

    public static async void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        LongitudeMax = mapData.Border.LongitudeMax;
        LatitudeMax = mapData.Border.LatitudeMax;
        LongitudeMin = mapData.Border.LongitudeMin;
        LatitudeMin = mapData.Border.LatitudeMin;
        var children = GetStartChildren(mapData);
        var fileName = mapData.MapName + ".txt";
        var male = File.Exists(fileName) ? ReadBestFromFile(fileName) : children[0];
        
        var female = children[1];
        var size = male.Length;

        var k = 0;
        foreach (var loc in mapData.locations)
            loc.Value.IndexKey = k++;
        foreach (var hs in mapData.Hotspots)
            hs.IndexKey = k++;
        var n = 0;
        var bestValue = 0d;
        (int Index, double Total) bestScore = default;
        (int Index, double Total)[] twoBest;
        var best = new ChildItem[size];
        
        while (true)
        {
            var maxHistory = new List<double>() { bestValue };
            children = GetStartChildren(mapData);
            while (true)
            {
                n++;
                MakeChildren(children, male, female);
                twoBest = Evaluate(children, mapData, generalData, false);
                male = children[twoBest[0].Index];
                female = children[twoBest[1].Index];

                var isBetter = twoBest[0].Total > bestValue;
                if (isBetter)
                {
                    bestValue = twoBest[0].Total;
                    bestScore = twoBest[0];
                    Array.Copy(children[twoBest[0].Index], best, best.Length);
                }

                if (n % 50 == 0)
                {
                    Console.WriteLine($"{n}. {bestScore.Total:0.##}pt");
                    var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                    if (betterThanPreviousBest)
                    {
                        maxHistory.Clear();
                        if (periodicSubmit)
                        {
                            // TODO: utred om det här borde göras parallellt
                            await Submit(mapData, best.Clone() as ChildItem[], bestValue);
                            File.WriteAllText(fileName, JsonConvert.SerializeObject(best));
                        }
                    }


                    maxHistory.Add(bestValue);
                    if (maxHistory.Count > 5)
                    {
                        Console.WriteLine("Restart");
                        break;
                    }
                }
            }
        }
    }

    private static ChildItem[][] GetStartChildren(MapData mapData)
    {
        var children = new ChildItem[ChildCount][];

        // var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall * h.Spread).ToArray(); // 2735
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall).ToArray(); //2737
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Spread).ToArray(); //2728
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall).ThenBy(x => x.Spread).ToArray(); //2737
        var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall / h.Spread).ToArray(); //2834

        for (int n = 0; n < ChildCount; n++)
        {
            var list = new List<ChildItem>();
            var h = 0;
            h = AddSpots(mapData, hots, list, h, Scoring.maxGroceryStoreLarge, 0);
            h = AddSpots(mapData, hots, list, h, Scoring.maxGroceryStore, 1);
            h = AddSpots(mapData, hots, list, h, Scoring.maxConvenience, 2);
            h = AddSpots(mapData, hots, list, h, Scoring.maxGasStation, 3);
            h = AddSpots(mapData, hots, list, h, Scoring.maxKiosk, 4);

            children[n] = list.ToArray();
        }

        return children;
    }

    private static int AddSpots(MapData mapData, Hotspot[] hots, List<ChildItem> list, int h, int max, int type)
    {
        for (int i = 0; i < max; i++)
        {
            var hotspot = hots[h++];
            var childItem = new ChildItem
            {
                F3100Count = Rnd.Next(MaxStations + 1),
                F9100Count = Rnd.Next(MaxStations + 1),
                Latitude = MoveInLat(hotspot.Latitude, mapData),
                Longitude = MoveInLon(hotspot.Longitude, mapData),
                LocationType = type
            };
            list.Add(childItem);
        }

        return h;
    }

    private static double MoveInLat(double latitude, MapData mapData)
    {
        if (latitude < mapData.Border.LatitudeMin)
            return mapData.Border.LatitudeMin;
        if (latitude > mapData.Border.LatitudeMax)
            return mapData.Border.LatitudeMax;
        return latitude;
    }

    private static double MoveInLon(double latitude, MapData mapData)
    {
        if (latitude < mapData.Border.LongitudeMin)
            return mapData.Border.LongitudeMin;
        if (latitude > mapData.Border.LongitudeMax)
            return mapData.Border.LongitudeMax;
        return latitude;
    }

    private static async Task Submit(MapData mapData, ChildItem[] best, double localScore)
    {
        SubmitSolution solution = new();
        for (var j = 0; j < best.Length; j++)
        {
            if (best[j].F3100Count > 0 || best[j].F9100Count > 0)
            {
                solution.Locations["location" + (j + 1)] = new PlacedLocations()
                {
                    Freestyle3100Count = best[j].F3100Count,
                    Freestyle9100Count = best[j].F9100Count,
                    Latitude = best[j].Latitude,
                    Longitude = best[j].Longitude,
                    LocationType = HelperExtensions.LocationTypes[best[j].LocationType].Value.Type
                };
            }
        }

        Scoring.SandboxValidation(mapData.MapName, solution, mapData);
        await SolutionBase.SubmitSolution(mapData, localScore, solution);
    }

    private static (int Index, double Total)[] Evaluate(
        ChildItem[][] children, MapData mapData, GeneralData generalData, bool distanceCache)
    {
        var topList = new List<(int Index, double Total)>();

        //for (int i = 0;i < children.Length;i++)
        Parallel.For(0, children.Length, (int i) =>
            {
                SubmitSolution solution = new();
                var child = children[i];
                for (var j = 0; j < children[i].Length; j++)
                {
                    if (child[j].F3100Count > 0 || child[j].F9100Count > 0)
                    {
                        solution.Locations["location" + (j + 1)] = new PlacedLocations()
                        {
                            Freestyle3100Count = child[j].F3100Count,
                            Freestyle9100Count = child[j].F9100Count,
                            Latitude = child[j].Latitude,
                            Longitude = child[j].Longitude,
                            LocationType = HelperExtensions.LocationTypes[child[j].LocationType].Value.Type,
                            IndexKey = mapData.Hotspots.Count + j + 1,
                        };
                    }
                }

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData, true, distanceCache);

                lock (topList)
                {
                    topList.Add((i, score));
                }
            }
        );

        return topList.OrderByDescending(x => x.Total).Take(2).ToArray();
    }

    private static void MakeChildren(ChildItem[][] children, ChildItem[] male, ChildItem[] female)
    {
        children[0] = male;
        children[1] = female;
        for (int i = 2; i < children.Length; i++)
        {
            var split = Rnd.Next(male.Length);
            Array.Copy(male, 0, children[i], 0, split);
            Array.Copy(female, split, children[i], split, female.Length - split);
            for (int m = 0; m < Mutations - 1; m++)
            {
                var mutation = Rnd.Next(male.Length);
                var what = Rnd.Next(3);
                if (what == 0) children[i][mutation].F3100Count = Rnd.Next(3);
                if (what == 1) children[i][mutation].F9100Count = Rnd.Next(3);
                if (what == 2)
                {
                    children[i][mutation].Latitude = RandomLatitude();
                    children[i][mutation].Longitude = RandomLongitude();
                }
            }
        }
    }

    private static double RandomLongitude()
    {
        return Rnd.NextDouble() * (LongitudeMax - LongitudeMin) + LongitudeMin;
    }

    private static double RandomLatitude()
    {
        return Rnd.NextDouble() * (LatitudeMax - LatitudeMax) + LatitudeMin;
    }

    private static ChildItem[] ReadBestFromFile(string fileName)
    {
        Console.WriteLine("Read " + fileName);
        return JsonConvert.DeserializeObject<ChildItem[]>(File.ReadAllText(fileName));
    }
}