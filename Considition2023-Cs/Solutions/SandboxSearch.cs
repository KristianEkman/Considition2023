using System.Text.Json.Nodes;
using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using Newtonsoft.Json;

public class SandboxSearch
{
    static Random Rnd = new(777);
    internal static int MaxStations = 2;
    internal static int ChildCount = 800;
    internal static int Mutations = 2;

    static int LongitudeMax = 0;
    static int LongitudeMin = 0;
    static int LatitudeMax = 0;
    static int LatitudeMin = 0;
    struct ChildItem { 
        public int F3100Count {get; set;}
        public int F9100Count{get; set;}
        public int LocationType{get; set;}
        public double Longitude{get; set;}
        public double Latitude{get; set;} 
    }

    public static async void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {      
        LongitudeMax = (int)mapData.Border.LatitudeMax;  
        LatitudeMax = (int)mapData.Border.LatitudeMax;
        LongitudeMin = (int)mapData.Border.LongitudeMin;
        LatitudeMin = (int)mapData.Border.LatitudeMin;
        var children = GetStartChildren(mapData);
        var fileName = mapData.MapName + ".txt";
        var male =  File.Exists(fileName) ? ReadBestFromFile(fileName) : children[0];        
        var female = children[1];
        var size = male.Length;
                
        Scoring.NewDistancesCache();
        var k = 0;
        foreach (var loc in mapData.locations)
            loc.Value.IndexKey = k++;
        foreach (var hs in mapData.Hotspots)
            hs.IndexKey = k++;
        
        var bestValue = 0d;
        (int Index, double Total, double Earnings, double KgCo2Savings, double Value) bestScore = default;
        (int Index, double Total, double Earnings, double KgCo2Savings, double Value)[] twoBest;
        var best = new ChildItem[size];
        var maxHistory = new List<double>() { bestValue };        
        var n = 0;
        while (true)
        {
            n++;
            MakeChildren(children, male, female);
            twoBest = Evaluate(children, mapData, generalData);
            male = children[twoBest[0].Index];
            female = children[twoBest[1].Index];

            var isBetter = twoBest[0].Value > bestValue;
            if (isBetter)
            {
                bestValue = twoBest[0].Value;
                bestScore = twoBest[0];
                Array.Copy(children[twoBest[0].Index], best, best.Length);
            }

            if (n % 10 == 0)
            {
                Console.WriteLine($"{n}. {bestScore.Total.ToSI()}pt, {bestScore.Earnings.ToSI()}kr, {bestScore.KgCo2Savings}kg");
                var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                if (betterThanPreviousBest)
                {
                    // maxHistory.Clear();
                    if (periodicSubmit)
                    {
                        await Submit(mapData, best.Clone() as ChildItem[], bestValue);
                        File.WriteAllText(fileName, JsonConvert.SerializeObject(best));
                    }
                }
            }
            // maxHistory.Add(bestValue);
            // if (maxHistory.Count > 5)
            // {
            //     var seed = Rnd.Next(1000);
            //     Console.WriteLine("New Seed: " + seed);
            //     Rnd = new Random(seed);
            //     maxHistory.Clear();
            //     maxHistory.Add(bestValue);
            // }            
        }
    }

    private static ChildItem[][] GetStartChildren(MapData mapData)
    {
        var children = new ChildItem[ChildCount][];        

        for (int n = 0; n < ChildCount; n++)
        {
            var list = new List<ChildItem>();
            var h = 0;
            for (int i = 0; i < Scoring.maxGroceryStoreLarge; i++)
            {
                var hotspot = mapData.Hotspots[h++];
                var childItem = new ChildItem {
                    F3100Count = Rnd.Next(MaxStations + 1),
                    F9100Count = Rnd.Next(MaxStations + 1),
                    Latitude = hotspot.Latitude,
                    Longitude = hotspot.Longitude,
                    LocationType = 0
                };                
                list.Add(childItem);
            }

            for (int i = 0; i < Scoring.maxGroceryStore; i++)
            {
                var hotspot = mapData.Hotspots[h++];
                var childItem = new ChildItem
                {
                    F3100Count = Rnd.Next(MaxStations + 1),
                    F9100Count = Rnd.Next(MaxStations + 1),
                    Latitude = hotspot.Latitude,
                    Longitude = hotspot.Longitude,
                    LocationType = 1
                };
                list.Add(childItem);
            }

            for (int i = 0; i < Scoring.maxConvenience; i++)
            {
                var hotspot = mapData.Hotspots[h++];
                var childItem = new ChildItem
                {
                    F3100Count = Rnd.Next(MaxStations + 1),
                    F9100Count = Rnd.Next(MaxStations + 1),
                    Latitude = hotspot.Latitude,
                    Longitude = hotspot.Longitude,
                    LocationType = 2
                };
                list.Add(childItem);
            }

            for (int i = 0; i < Scoring.maxGasStation; i++)
            {
                var hotspot = mapData.Hotspots[h++];
                var childItem = new ChildItem
                {
                    F3100Count = Rnd.Next(MaxStations + 1),
                    F9100Count = Rnd.Next(MaxStations + 1),
                    Latitude = hotspot.Latitude,
                    Longitude = hotspot.Longitude,
                    LocationType = 3
                };
                list.Add(childItem);
            }

            for (int i = 0; i < Scoring.maxKiosk; i++)
            {
                var hotspot = mapData.Hotspots[h++];
                var childItem = new ChildItem
                {
                    F3100Count = Rnd.Next(MaxStations + 1),
                    F9100Count = Rnd.Next(MaxStations + 1),
                    Latitude = hotspot.Latitude,
                    Longitude = hotspot.Longitude,
                    LocationType = 4
                };
                list.Add(childItem);
            }

            children[n] = list.ToArray();
        }

        return children;
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

    private static (int Index, double Total, double Earnings, double KgCo2Savings, double v)[] Evaluate(
        ChildItem[][] children, MapData mapData, GeneralData generalData)
    {
        var topList = new List<(int Index, double Total, double Earnings, double KgCo2Savings, double v)>();
        
        for (int i = 0;i < children.Length;i++)
        //Parallel.For(0, children.Length, (int i) =>
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

                var score = Scoring.CalculateScore("", solution, mapData, generalData, true);
                var v = score.GameScore.Total;
                lock (topList)
                {
                    topList.Add((i, score.GameScore.Total, score.GameScore.Earnings, score.GameScore.KgCo2Savings, v));
                }
            }
        //);

        return topList.OrderByDescending(x => x.v).Take(2).ToArray();
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
                if (what == 0) children[i][mutation].F3100Count = Rnd.Next(MaxStations + 1);
                if (what == 1) children[i][mutation].F9100Count = Rnd.Next(MaxStations + 1);
                if (what == 2) {
                    children[i][mutation].Latitude -= Rnd.Next(LatitudeMin, LatitudeMax);
                    children[i][mutation].Longitude -= Rnd.Next(LongitudeMin, LongitudeMax);
                } 
            }
            // TODO: also mutate other features
        }
    }
    private static ChildItem[] ReadBestFromFile(string fileName)
    {
        Console.WriteLine("Read " + fileName);
        return JsonConvert.DeserializeObject<ChildItem[]>(File.ReadAllText(fileName));        
    }
}