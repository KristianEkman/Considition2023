using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using System.Diagnostics;
using Newtonsoft.Json;

public enum SelectionMode
{
    Original,
    Roulette,
}
public enum CrossoverMode
{
    TopBottom,
    Range,
}

public enum MutationMode
{
    NumMutations,
    PercentChance,
}

public class GeneticSearchFaster
{
    static Random Rnd = new();
    const int MaxStations = 2;
    internal static int ChildCount = 500;
    internal static bool Rounding = false;
    internal static SelectionMode SelectionMode = SelectionMode.Roulette;
    internal static int KeepNBest = 4;
    internal static int IntroduceNRandom = 5;
    internal static CrossoverMode CrossoverMode = CrossoverMode.Range;
    internal static MutationMode MutationMode = MutationMode.PercentChance;
    internal static int Mutations = 2;
    internal static double MutationChance = 0.01;

    public static void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        var size = mapData.locations.Count;
        Console.WriteLine($"{mapData.MapName} has {size} locations");
        DistanceCache.Reset(size);
        var n = 0;
        var bestValue = 0d;
        var fileName = mapData.MapName + "_m.txt";
        
        while (true)
        {
            var generationA = new (int, int)[ChildCount][];
            var generationB = new (int, int)[ChildCount][];

            var stored = ReadBestFromFile(fileName);
            for (var i = 0; i < stored.Count; i++)
            {
                generationA[i] = stored[i].Genome;
                generationB[i] = new (int, int)[size];
            }
            for (var i = stored.Count; i < ChildCount; i++)
            {
                generationA[i] = RandomArray(size);
                generationB[i] = new (int, int)[size];
            }
            
            var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
            var k = 0;
            foreach (var loc in mapData.locations)
                loc.Value.IndexKey = k++;
            foreach (var hs in mapData.Hotspots)
                hs.IndexKey = k++;
            
            (int Index, double Total)[] twoBest;
            var best = new (int, int)[size];
            var maxHistory = new List<double>() { bestValue };

            var stopWatch = Stopwatch.StartNew();
            var odd = false;
            while (true)
            {
                n++;
                odd = !odd;
                var current = odd ? generationA : generationB;
                var children = odd ? generationB : generationA;

                var results = Evaluate(current, mapData, generalData);
                
                var isBetter = results[0].Total > bestValue;
                if (isBetter)
                {
                    bestValue = results[0].Total;
                    Array.Copy(current[results[0].Index], best, best.Length);
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

                            if (!stored.Any(x => x.Genome.SequenceEqual(best)))
                            {
                                stored.Add(new Stored
                                {
                                    Genome = best,
                                    Score = bestValue,
                                });
                                File.WriteAllText(fileName, JsonConvert.SerializeObject(stored));
                            }
                        }
                    }
                    maxHistory.Add(bestValue);
                    if (maxHistory.Count > 5)
                    {
                        var seed = Rnd.Next(ChildCount);
                        Rnd = new Random(seed);
                        Console.WriteLine($"Restart with {ChildCount} children. Seed {seed}");

                        break;
                        
                        //maxHistory.Clear();
                        //maxHistory.Add(bestValue);
                    }
                }
                
                MakeChildren(current, results, children);
            }
        }
    }

    public class Stored
    { 
        public double Score { get; set; }
        public (int, int)[] Genome { get; set; }
    }
    private static List<Stored> ReadBestFromFile(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return new List<Stored>();
        }
        
        Console.WriteLine("Reading " + fileName);
        var json = File.ReadAllText(fileName);
        var items = JsonConvert.DeserializeObject<List<Stored>>(json);
        return items;
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

    private static (int Index, double Total) [] Evaluate((int, int)[][] children, MapData mapData, GeneralData generalData) {
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

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData, false, true, Rounding);                
                lock (topList)
                {
                    topList.Add((i, score));
                }
            }
        );

        return topList.OrderByDescending(x => x.Item2).ToArray();
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

    private static void MakeChildren((int, int)[][] current, (int, double) [] results, (int, int)[][] children)
    {
        switch (SelectionMode)
        {
            case SelectionMode.Original:
                OriginalSelection(current, results, children);
                break;
            case SelectionMode.Roulette:
                RouletteSelection(current, results, children);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
    }

    private static void RouletteSelection((int, int)[][] current, (int Index, double Total) [] results, (int, int)[][] children)
    {
        int Select(double totalScore)
        {
            var rnd = Rnd.NextDouble() * totalScore;
            var tally = 0d;
            var i = 0;
            while (tally <= rnd)
            {
                tally += results[i++].Total;
            }
            return i - 1;
        }
        
        var totalScore = results.Sum(x => x.Total);
        
        var combinations = new (int Parent1Index, int Parent2Index)[children.Length - KeepNBest - IntroduceNRandom];
        for (var i = 0; i < combinations.Length; i++)
        {
            var p1 = Select(totalScore);
            int p2;
            do
            {
                p2 = Select(totalScore);
            } while (p1 == p2);
            combinations[i] = (p1, p2);
        }

        for (var i = 0; i < children.Length; i++)
        {
            if (i < combinations.Length)
            {
                var p1 = current[combinations[i].Parent1Index];
                var p2 = current[combinations[i].Parent2Index];
                Cross(p1, p2, children[i]);
                Mutate(children[i]);
            } else if (i < combinations.Length + KeepNBest)
            {
                Array.Copy(current[results[i - combinations.Length].Index], children[i], children[i].Length);
            }  else
            {
                children[i] = RandomArray(current[0].Length);
            }
            
        }
    }
    private static void OriginalSelection((int, int)[][] current, (int Index, double Total) [] results, (int, int)[][] children)
    {
        var male = children[0] = current[results[0].Index];
        var female = children[1] = current[results[1].Index];;
        for (int i = 2; i < children.Length; i++)
        {
            Cross(male, female, children[i]);
            Mutate(children[i]);
        }
    }

    private static void Mutate((int, int)[] child)
    {
        switch (MutationMode)
        {
            case MutationMode.NumMutations:
                MutateNMutations(child);
                break;
            case MutationMode.PercentChance:
                MutatePercentChance(child);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
    }
    private static void MutateNMutations((int, int)[] child)
    {
        for (int m = 0; m < Mutations; m++)
        {
            var mutation = Rnd.Next(child.Length);
            child[mutation] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
        }
    }
    private static void MutatePercentChance((int, int)[] child)
    {
        for (var i = 0; i < child.Length; i++)
        {
            if (Rnd.NextDouble() < MutationChance)
            {
                child[i] = (Rnd.Next(MaxStations + 1), Rnd.Next(MaxStations + 1));
            }
        }
    }

    private static void Cross((int, int)[] parent1, (int, int)[] parent2, (int, int)[] child)
    {
        switch (CrossoverMode)
        {
            case CrossoverMode.TopBottom:
                TopBottomCross(parent1, parent2, child);
                break;
            case CrossoverMode.Range:
                RangeCross(parent1, parent2, child);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    private static void TopBottomCross((int, int)[] parent1, (int, int)[] parent2, (int, int)[] child)
    {
        var split = Rnd.Next(parent1.Length);
        Array.Copy(parent1, 0, child, 0, split);
        Array.Copy(parent2, split, child, split, parent2.Length - split);
    }
    private static void RangeCross((int, int)[] parent1, (int, int)[] parent2, (int, int)[] child)
    {
        var split = Rnd.Next(parent1.Length);
        var length = Rnd.Next(parent1.Length - split);
        Array.Copy(parent1, 0, child, 0, split);
        Array.Copy(parent2, split, child, split, length);
        Array.Copy(parent1, split + length, child, split + length, parent1.Length - split - length);
    }
}