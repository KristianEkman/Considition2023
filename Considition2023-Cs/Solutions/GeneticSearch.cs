using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using Newtonsoft.Json;

public class GeneticSearch
{
    static Random Rnd = new(777);
    internal static int MaxStations = 2;
    internal static int ChildCount = 400;    
    internal static int Mutations = 3;
    internal static bool Rounding = false;

    public static void Run(MapData mapData, GeneralData generalData, bool periodicSubmit, Func<Score, double> optimizeFor, bool optimizeLow)
    {
        var size = mapData.locations.Count;
        var fileName = mapData.MapName + ".txt";
        var male =  File.Exists(fileName) ? ReadBestFromFile(fileName) : RandomArray(size);
        var female = RandomArray(size);
        var children = new (int, int)[ChildCount][];
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
        DistanceCache.Reset(size);
        var k = 0;
        foreach (var loc in mapData.locations)
            loc.Value.IndexKey = k++;        
        foreach (var hs in mapData.Hotspots)
            hs.IndexKey = k++;

        for (int i = 0; i < ChildCount; i++)
        {
            children[i] = new (int, int)[size];
        }

        var bestValue = optimizeLow ? double.MaxValue : 0d;
        (int Index, double Total, double Earnings, double KgCo2Savings, double v) bestScore = default;
        (int Index, double Total, double Earnings, double KgCo2Savings, double v)[] twoBest;
        var best = new (int, int)[size];
        var maxHistory = new List<double>() { bestValue };
        //for (int n = 0; n < Runs; n++)
        var n = 0;
        while (true)
        {
            n++;
            MakeChildren(children, male, female);
            twoBest = Evaluate(children, mapData, generalData, optimizeFor, optimizeLow);
            male = children[twoBest[0].Index];
            female = children[twoBest[1].Index];

            var isBetter = optimizeLow
                ? twoBest[0].v < bestValue
                : twoBest[0].v > bestValue;
            if (isBetter)
            {
                bestValue = twoBest[0].v;
                bestScore = twoBest[0];
                Array.Copy(children[twoBest[0].Index], best, best.Length);

                if (optimizeLow && bestValue == 0d)
                {
                    // found the target score
                    Submit(mapData, names, best.Clone() as (int, int)[], bestValue);
                    break;
                }
            }

            if (n % 100 == 0)
            {
                Console.WriteLine($"{n}. {bestScore.Total.ToSI()}pt, {bestScore.Earnings.ToSI()}kr, {bestScore.KgCo2Savings}kg");
                var betterThanPreviousBest = optimizeLow
                    ? bestValue < maxHistory.LastOrDefault()
                    : bestValue > maxHistory.LastOrDefault();
                if (betterThanPreviousBest)
                {
                    maxHistory.Clear();
                    if (periodicSubmit)
                    {
                        Submit(mapData, names, best.Clone() as (int, int)[], bestValue);
                        File.WriteAllText(mapData.MapName + ".txt", JsonConvert.SerializeObject(best));
                    }
                }
                maxHistory.Add(bestValue);
                if (maxHistory.Count > 5)
                {
                    var seed = Rnd.Next(1000);
                    Console.WriteLine("New Seed: " + seed);
                    Rnd = new Random(seed);
                    maxHistory.Clear();
                    maxHistory.Add(bestValue);
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

    private static void Submit(MapData mapData, string[] names, (int, int)[] best, double localScore)
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

        SolutionBase.SubmitSolutionAsync(mapData, localScore, solution);
    }    

    private static (int Index, double Total, double Earnings, double KgCo2Savings, double v)[] Evaluate(
        (int, int)[][] children, MapData mapData, GeneralData generalData, Func<Score, double> optimizeFor, bool optimizeLow)
    {
        var topList = new List<(int Index, double Total, double Earnings, double KgCo2Savings, double v)>();
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

                var score = Scoring.CalculateScore("", solution, mapData, generalData);
                var v = optimizeFor(score.GameScore);
                lock (topList)
                {
                    topList.Add((i, score.GameScore.Total, score.GameScore.Earnings, score.GameScore.KgCo2Savings, v));
                }
            }
        );

        return optimizeLow
            ? topList.OrderBy(x => x.v).Take(2).ToArray()
            : topList.OrderByDescending(x => x.v).Take(2).ToArray();
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
            for (int m = 0; m < Mutations - 1; m++)
            {
                var mutation = Rnd.Next(male.Length);
                children[i][mutation] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
            }
        }
    }
}