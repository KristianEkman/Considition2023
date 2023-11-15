using Considition2023_Cs;

public class GeneticSearch
{
    static Random Rnd = new(777);
    const int MaxStations = 3;
    const int childCount = 500;    
    const int Mutations = 2;

    public static async void Run(MapData mapData, GeneralData generalData, bool periodicSubmit, Func<Score, double> optimizeFor, bool optimizeLow)
    {
        var size = mapData.locations.Count;
        var male = RandomArray(size);
        var female = RandomArray(size);
        var children = new (int, int)[childCount][];
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
        Scoring.NewDistancesCache();
        var k = 0;
        foreach (var loc in mapData.locations)
            loc.Value.IndexKey = k++;        
        foreach (var hs in mapData.Hotspots)
            hs.IndexKey = k++;

        for (int i = 0; i < childCount; i++)
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
                    await Submit(mapData, names, best.Clone() as (int, int)[]);
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
                        Submit(mapData, names, best.Clone() as (int, int)[]);
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

    private static async Task Submit(MapData mapData, string[] names, (int, int)[] best)
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

        HttpClient client = new();
        Api api = new(client);
        GameData prodScore = await api.SumbitAsync(mapData.MapName, solution, HelperExtensions.Apikey);
        var result = $"\r\n{mapData.MapName}: {prodScore.Id} {prodScore.GameScore.Total.ToSI()} at {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")}";
        Console.WriteLine(result);
        File.AppendAllText("resultslog.txt", result);

        var F3100count = solution.Locations.GroupBy(x => x.Value.Freestyle3100Count).OrderBy(x => x.Key);
        Console.WriteLine("F3100");
        foreach (var item in F3100count)
        {
            Console.WriteLine($"{item.Key}st: {item.Count()} places");
        }

        var F9100count = solution.Locations.GroupBy(x => x.Value.Freestyle9100Count).OrderBy(x => x.Key);
        Console.WriteLine("\nF9100");
        foreach (var item in F9100count)
        {
            Console.WriteLine($"{item.Key}st: {item.Count()}");
        }
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
            a[i] = (Rnd.Next(MaxStations), Rnd.Next(MaxStations));
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
            for (int m = 0; m < Rnd.Next(Mutations); m++)
            {
                var mutation = Rnd.Next(male.Length);
                children[i][mutation] = (Rnd.Next(MaxStations), Rnd.Next(MaxStations));
            }
        }
    }
}