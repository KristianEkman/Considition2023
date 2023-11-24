using System.Diagnostics;
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
    internal static bool Rounding = false;
    internal static int MoveStep = 100;


    static double LongitudeMax = 0;
    static double LongitudeMin = 0;
    static double LatitudeMax = 0;
    static double LatitudeMin = 0;
    static double[] LatStep;
    static double[] LongStep;

    struct ChildItem
    {
        public int F3100Count { get; set; }
        public int F9100Count { get; set; }
        public int LocationType { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
    }

    public static void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        LongitudeMax = mapData.Border.LongitudeMax;
        LatitudeMax = mapData.Border.LatitudeMax;
        LongitudeMin = mapData.Border.LongitudeMin;
        LatitudeMin = mapData.Border.LatitudeMin;
        LatStep = [(LatitudeMax - LatitudeMin) / MoveStep, -(LatitudeMax - LatitudeMin) / MoveStep];
        LongStep = [(LongitudeMax - LongitudeMin) / MoveStep, -(LongitudeMax - LongitudeMin) / MoveStep];

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
        var stopWatch = Stopwatch.StartNew();


        while (true)
        {
            var maxHistory = new List<double>() { bestValue };
            children = GetStartChildren(mapData);
            female = children[1];
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
                    var speed = ((50 * ChildCount) / (double)stopWatch.ElapsedMilliseconds).ToString("0.##");
                    stopWatch.Restart();
                    Console.WriteLine($"{n}. {bestScore.Total:0.##}pt\t{speed} evs/ms");
                    var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                    if (betterThanPreviousBest)
                    {
                        maxHistory.Clear();
                        if (periodicSubmit)
                        {
                            JsonConvert.SerializeObject(best).SaveTo(fileName);
                            Submit(mapData, best.Clone() as ChildItem[], bestValue);
                        }
                    }

                    maxHistory.Add(bestValue);
                    if (maxHistory.Count > 2)
                    {
                        var seed = Rnd.Next(ChildCount);
                        Rnd = new Random(seed);
                        Console.WriteLine($"Restart with {ChildCount} children. Seed {seed}");
                        break;
                    }
                }
            }
        }
    }

    private static ChildItem[][] GetStartChildren(MapData mapData)
    {
        var locations = new Dictionary<int, StoreLocationScoring>();

        var children = new ChildItem[ChildCount][];

        // var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall * h.Spread).ToArray(); // 2735
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall).ToArray(); //2737
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Spread).ToArray(); //2728
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall).ThenBy(x => x.Spread).ToArray(); //2737
        //var hots = mapData.Hotspots.OrderByDescending(h => h.Footfall / h.Spread).ToArray(); //3184
        var hots = mapData.Hotspots.OrderByDescending(h => (h.Footfall * h.Footfall) / h.Spread).ToArray(); //

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
            var hotspot = hots[Rnd.Next(hots.Length)];
            var childItem = new ChildItem
            {
                F3100Count = Rnd.Next(MaxStations + 1),
                F9100Count = Rnd.Next(MaxStations + 1),
                Latitude = MoveInLat(hotspot.Latitude),
                Longitude = MoveInLon(hotspot.Longitude),
                LocationType = type
            };
            list.Add(childItem);
        }

        return h;
    }

    private static double MoveInLat(double latitude)
    {
        if (latitude < LatitudeMin)
            return LatitudeMin;
        if (latitude > LatitudeMax)
            return LatitudeMax;
        return latitude;
    }

    private static double MoveInLon(double longitude)
    {
        if (longitude < LongitudeMin)
            return LongitudeMin;
        if (longitude > LongitudeMax)
            return LongitudeMax;
        return longitude;
    }

    private static void Submit(MapData mapData, ChildItem[] best, double localScore)
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
        SolutionBase.SubmitSolutionAsync(mapData, localScore, solution);
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

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData, true, distanceCache, Rounding);

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
        for (int i = 1; i < children.Length; i++)
        {
            var split = Rnd.Next(male.Length);
            Array.Copy(male, 0, children[i], 0, split);
            Array.Copy(female, split, children[i], split, female.Length - split);
            for (int m = 0; m < Mutations - 1; m++)
            {
                var mutation = Rnd.Next(male.Length);
                var what = 0;// Rnd.Next(2);
                if (what == 0)
                {
                    children[i][mutation].F3100Count = Rnd.Next(3);
                    children[i][mutation].F9100Count = Rnd.Next(3);
                }
                else
                {
                    children[i][mutation].Latitude = MoveInLat(children[i][mutation].Latitude + RandomLatitude());
                    children[i][mutation].Longitude = MoveInLon(children[i][mutation].Longitude + RandomLongitude());
                }
            }
        }
    }

    private static double RandomLongitude()
    {
        var i = Rnd.Next(0, 1);
        return LongStep[i] * Rnd.NextDouble();
    }

    private static double RandomLatitude()
    {
        var i = Rnd.Next(0, 1);
        return LatStep[i] * Rnd.NextDouble();
    }

    private static ChildItem[] ReadBestFromFile(string fileName)
    {
        Console.WriteLine("Read " + fileName);
        return JsonConvert.DeserializeObject<ChildItem[]>(fileName.ReadFileText());
    }
}