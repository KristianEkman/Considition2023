using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using System.Diagnostics;

public class GeneticSearchFaster
{
    static Random Rnd = new(777);
    const int MaxStations = 2;
    internal static int ChildCount = 500;
    internal static int Mutations = 2;
    internal static bool Rounding = false;
    internal static bool UseHotSpots = false;
    internal static int[] HotSpots;
    internal static DateTime StartTime = DateTime.Now;

    public static void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        var mapName = mapData.MapName;
        var size = mapData.locations.Count;
        Console.WriteLine($"{mapName} has {size} locations");
        DistanceCache.Reset(size);

        var dir = new DirectoryInfo(mapName);
        if (!dir.Exists)
            dir.Create();

        if (UseHotSpots)
            StoreHotSpots(mapName);

        var n = 0;
        var bestValue = 0d;
        var fileName = mapName + ".txt";

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
                if (UseHotSpots)
                    MakeChildrenOfHotspots(children, male, female);
                else
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
                    var duration = (DateTime.Now - StartTime).TotalSeconds.ToString("0.#");
                    stopWatch.Restart();
                    Console.WriteLine($"{n}. {bestValue:0.##}pt after {duration}s\t{speed} evs/ms");
                    var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                    if (betterThanPreviousBest)
                    {
                        maxHistory.Clear();
                        if (periodicSubmit)
                        {
                            SaveFiles(mapName, bestValue, best);
                            Submit(mapData, names, best.Clone() as (int, int)[], bestValue);
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

    private static void SaveFiles(string name, double bestValue, (int, int)[] best)
    {
        var data = string.Join(";", best);
        File.WriteAllText($"{name}.txt", data);
        var scoreText = bestValue.ToString("0.##").Replace(",", ".");
        var path = Path.Combine(name, name + $"{scoreText}.txt");
        File.WriteAllText(path, data);
    }

    private static void StoreHotSpots(string mapName)
    {
        var files = Directory.GetFiles(mapName);
        var datas = new List<(int, int)[]>();
        foreach (var file in files)
        {
            datas.Add(ReadBestFromFile(file));
        }
        var list = new List<int>();
        var n = 0;
        do
        {
            for (int i = 0; i < datas.Count - 1; i++)
            {
                if (datas[i][n].Item1 != datas[i + 1][n].Item1 || datas[i][n].Item2 != datas[i + 1][n].Item2)
                    list.Add(n);
            }
            n++;
        } while (n < datas[0].Length);
        HotSpots = list.Distinct().ToArray();
        Console.WriteLine("Hotspots: " + HotSpots.Length);
    }

    private static (int, int)[] ReadBestFromFile(string fileName)
    {
        Console.WriteLine("Reading " + fileName);
        var items = File.ReadAllText(fileName).Split(";");
        var list = new List<(int, int)>();
        foreach (var item in items)
        {
            var vals = item.Replace("(", "").Replace(")", "").Split(",");
            list.Add((int.Parse(vals[0].Trim()), int.Parse(vals[1].Trim())));
        }

        var mutationKind = list.Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2);
        Console.WriteLine(string.Join(",", mutationKind));
        //GoodMutations = mutationKind.ToArray();
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

    private static (int, double)[] Evaluate((int, int)[][] children, MapData mapData, GeneralData generalData)
    {
        var topList = new List<(int, double)>();
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();

        //for (int i = 0;i < children.Length;i++)
        Parallel.For(0, children.Length, (int i) =>
            {
                //Thread.CurrentThread.Priority = ThreadPriority.Highest;
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

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData, false, true, Rounding);
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
            a[i] = GoodMutations[Rnd.Next(GoodMutations.Length)];
        }
        return a;
    }

    private static (int, int)[] GoodMutations = [(0, 0), (0, 1), (1, 0), (2, 0), (0, 2)];
    private static void MakeChildren((int, int)[][] children, (int, int)[] male, (int, int)[] female)
    {
        children[0] = male;
        children[1] = female;
        for (int i = 1; i < children.Length; i++)
        {
            var split = Rnd.Next(male.Length); //(i - 1) % male.Length; Intressant att ett slumptal h�r ger mycket b�ttre inl�rning �n glidande start.
            var length = female.Length - split; // Math.Min((int)(male.Length * 0.25d), female.Length - split);
            Array.Copy(male, 0, children[i], 0, split);
            Array.Copy(female, split, children[i], split, length);
            for (int m = 0; m < Mutations; m++)
            {
                var mutation = Rnd.Next(male.Length);
                var v1 = GoodMutations[Rnd.Next(GoodMutations.Length)];
                children[i][mutation] = v1;
            }
        }
    }

    private static void MakeChildrenOfHotspots((int, int)[][] children, (int, int)[] male, (int, int)[] female)
    {
        children[0] = male;
        children[1] = female;
        for (int i = 1; i < children.Length; i++)
        {
            Array.Copy(male, 0, children[i], 0, male.Length);
            for (int m = 0; m < Mutations; m++)
            {
                var mutation = HotSpots[Rnd.Next(HotSpots.Length)];
                do
                {
                    children[i][mutation] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
                } while (children[i][mutation].Item1 > 0 && children[i][mutation].Item2 > 0);
            }
        }
    }
}