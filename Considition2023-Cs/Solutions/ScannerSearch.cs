using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using System.Diagnostics;

public class ScannerSearch
{
    static Random Rnd = new(777);

    public static void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        var mapName = mapData.MapName;
        var StartTime = DateTime.Now;
        var size = mapData.locations.Count;
        Console.WriteLine($"{mapName} has {size} locations");
        DistanceCache.Reset(size);

        var dir = new DirectoryInfo(Path.Combine("saved", mapName));
        if (!dir.Exists)
            dir.Create();

        var n = 0;
        var bestValue = 0d;
        var fileName = Path.Combine("saved", mapName + ".txt");

        var pairs = File.Exists(fileName) ? ReadBestFromFile(fileName) : new (int, int)[size];

        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();

        var maxHistory = new List<double>() { bestValue };

        var stopWatch = Stopwatch.StartNew();
        for (int c = 0; c < size; c++)
        {
            for (int m = 0; m < GoodMutations.Length; m++)
            {
                var old = pairs[c];
                pairs[c] = GoodMutations[m];

                n++;
                var score = Evaluate(pairs, mapData, generalData);
                var isBetter = score > bestValue;
                if (isBetter)
                {
                    bestValue = score;
                }
                else
                {
                    pairs[c] = old;
                }

                if (n % 100 == 0)
                {
                    var speed = (100 / (double)stopWatch.ElapsedMilliseconds).ToString("0.##");
                    var duration = (DateTime.Now - StartTime).TotalSeconds.ToString("0.#");
                    stopWatch.Restart();
                    Console.WriteLine($"{n}. {bestValue:0.##}pt after {duration}s\t{speed} evs/ms");
                    var betterThanPreviousBest = bestValue > maxHistory.LastOrDefault();
                    if (betterThanPreviousBest)
                    {
                        maxHistory.Clear();
                        if (periodicSubmit)
                        {
                            SaveFiles(mapName, bestValue, pairs);
                            Submit(mapData, names, pairs.Clone() as (int, int)[], bestValue);
                        }
                    }
                    maxHistory.Add(bestValue);
                }
            }
        }
    }

    private static void SaveFiles(string name, double bestValue, (int, int)[] best)
    {
        var data = string.Join(";", best);
        data.SaveTo($"{name}.txt");

        var scoreText = bestValue.ToString("0.##").Replace(",", ".");
        var path = Path.Combine(name, name + $"{scoreText}.txt");
        data.SaveTo(path);
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

    private static double Evaluate((int, int)[] children, MapData mapData, GeneralData generalData)
    {
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();

        SubmitSolution solution = new();
        for (var j = 0; j < children.Length; j++)
        {
            var child = children[j];

            if (child.Item1 > 0 || child.Item2 > 0)
            {
                solution.Locations[names[j]] = new PlacedLocations()
                {
                    Freestyle3100Count = child.Item1,
                    Freestyle9100Count = child.Item2
                };
            }
        }

        var score = ScoringFaster.CalculateScore(solution, mapData, generalData, false, true, true);
        return score;

    }

    private static (int, int)[] GoodMutations = [(0, 0), (0, 1), (1, 0), (2, 0), (0, 2)];


}