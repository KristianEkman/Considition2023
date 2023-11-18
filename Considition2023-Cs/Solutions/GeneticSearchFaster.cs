using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using System.Diagnostics;
using System.Text.Json;

public class GeneticSearchFaster
{
    static Random Rnd = new(777);
    const int MaxStations = 2;
    internal static int ChildCount = 500;
    internal static int Mutations = 1;

    public static async void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        var size = mapData.locations.Count;
        Console.WriteLine($"{mapData.MapName} has {size} locations");
        DistanceCache.Reset();
        var n = 0;
        var bestValue = 0d;
        var fileName = mapData.MapName + ".txt";

        while (true)
        {
            var male = File.Exists(fileName) ? ReadBestFromFile(fileName) : RandomArray(size);
            var female = RandomArray(size);
            var children = new (int, int)[ChildCount][];
            var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
            var k = 0;
            foreach (var loc in mapData.locations)
                loc.Value.IndexKey = k++;
            foreach (var hs in mapData.Hotspots)
                hs.IndexKey = k++;

            for (int i = 0; i < ChildCount; i++)
            {
                children[i] = new (int, int)[size];
            }

            (int Index, double Total)[] twoBest;
            var best = new (int, int)[size];
            var maxHistory = new List<double>() { bestValue };

            var stopWatch = Stopwatch.StartNew();
            while (true)
            {
                n++;
                MakeChildren(children, male, female);
                twoBest = Evaluate(children, mapData, generalData);
                male = children[twoBest[0].Index];
                female = children[twoBest[1].Index];

                var isBetter = twoBest[0].Total > bestValue;
                if (isBetter)
                {
                    bestValue = twoBest[0].Total;
                    Array.Copy(children[twoBest[0].Index], best, best.Length);
                }

                if (n % 100 == 0)
                {
                    var speed = ((100 * ChildCount) / (double)stopWatch.ElapsedMilliseconds).ToString("0.##");
                    stopWatch.Restart();
                    Console.WriteLine($"{n}. {bestValue.ToString("0.##")} pt\t{speed} evs/ms");
                    var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                    if (betterThanPreviousBest)
                    {
                        maxHistory.Clear();
                        if (periodicSubmit)
                        {
                            Submit(mapData, names, best.Clone() as (int, int)[], bestValue);
                            File.WriteAllText(mapData.MapName + ".txt", string.Join(";", best));
                        }
                    }
                    maxHistory.Add(bestValue);
                    if (maxHistory.Count > 5)
                    {
                        Console.WriteLine("Restart");
                        break;
                        //var seed = Rnd.Next(1000);
                        //Console.WriteLine("New Seed: " + seed);
                        //Rnd = new Random(seed);
                        //maxHistory.Clear();
                        //maxHistory.Add(bestValue);
                    }
                }
            }
        }
    }

    private static (int, int)[] ReadBestFromFile(string fileName)
    {
        var items = File.ReadAllText(fileName).Split(";");
        var list = new List<(int, int)>();
        foreach (var item in items)
        {
            var vals = item.Replace("(", "").Replace(")", "").Split(",");
            list.Add((int.Parse(vals[0].Trim()), int.Parse(vals[1].Trim())));
        }
        return list.ToArray();
    }

    private static async Task Submit(MapData mapData, string[] names, (int, int)[] best, double localScore)
    {
        SubmitSolution solution = new();
        for (var j = 0; j < best.Length; j++)
        {
            if (best[j].Item1 > 0 || best[j].Item2 > 0)
            {
                solution.Locations[names[j]] = new PlacedLocations()
                {
                    Freestyle3100Count = best[j].Item1,
                    Freestyle9100Count = best[j].Item2
                };
            }
        }

        await  SolutionBase.SubmitSolution(mapData, localScore, solution);
    }    

    private static (int, double) [] Evaluate((int, int)[][] children, MapData mapData, GeneralData generalData) {
        var topList = new List<(int, double)>();
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();

        //for (int i = 0;i < children.Length;i++)
        Parallel.For(0, children.Length, (int i) =>
            {
                SubmitSolution solution = new();
                var child = children[i];
                for (var j = 0; j < children[i].Length; j++)
                {
                    if (child[j].Item1 > 0 || child[j].Item2 > 0)
                    {
                        solution.Locations[names[j]] = new PlacedLocations()
                        {
                            Freestyle3100Count = child[j].Item1,
                            Freestyle9100Count = child[j].Item2
                        };
                    }
                }

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData);                
                lock (topList)
                {
                    topList.Add((i, score));
                }
            }
        );

        return topList.OrderByDescending(x => x.Item2).Take(2).ToArray();
    }

    private static (int, int)[] RandomArray(int size)
    {

        (int, int)[] a = new (int, int)[size];
        for (int i = 0; i < size; i++)
        {
            a[i] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
        }
        return a;
    }

    private static void MakeChildren((int, int)[][] children, (int, int)[] male, (int, int)[] female)
    {
        children[0] = male;
        children[1] = female;
        for (int i = 2; i < children.Length; i++)
        {
            var split = Rnd.Next(male.Length);
            Array.Copy(male, 0, children[i], 0, split);
            Array.Copy(female, split, children[i], split, female.Length - split);
            for (int m = 0; m < Mutations; m++)
            {
                var mutation = Rnd.Next(male.Length);
                children[i][mutation] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
            }
        }
    }
}