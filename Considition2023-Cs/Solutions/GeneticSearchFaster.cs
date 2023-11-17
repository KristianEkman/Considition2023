using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using Newtonsoft.Json;
using System.Diagnostics;

public class GeneticSearchFaster
{
    static Random Rnd = new(777);
    internal static int MaxStations = 2;
    internal static int ChildCount = 1000;
    internal static int Mutations = 1;

    public static async void Run(MapData mapData, GeneralData generalData, bool periodicSubmit)
    {
        var locationCount = mapData.locations.Count;
        Console.WriteLine($"{mapData.MapName} has {locationCount} locations");
        Scoring.NewDistancesCache();
        var n = 0;
        var bestValue = 0d;
        var fileName = mapData.MapName + ".txt";
        var selection = (int)(ChildCount * 0.9);

        while (true)
        {
            var bestChild = File.Exists(fileName) ? ReadBestFromFile(fileName) : RandomChild(locationCount);
            var children = new Child[ChildCount];
            for ( int i = 0; i < ChildCount; i++ )
                children[i] = RandomChild(locationCount);
            children[0] = bestChild;

            var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
            var k = 0;
            foreach (var loc in mapData.locations)
                loc.Value.IndexKey = k++;
            foreach (var hs in mapData.Hotspots)
                hs.IndexKey = k++;                       

            var maxHistory = new List<double>() { bestValue };

            var stopWatch = Stopwatch.StartNew();
            while (true)
            {
                n++;
                MakeChildren(children, 0, Rnd.Next(selection));
                children = EvaluateAndOrder(children, mapData, generalData);

                var isBetter = children[0].Score > bestValue;
                if (isBetter)
                {
                    bestValue = children[0].Score;
                    bestChild = children[0].Copy();
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
                            Submit(mapData, names, bestChild, bestValue);
                            File.WriteAllText(mapData.MapName + ".txt", JsonConvert.SerializeObject(children[0]));
                        }
                    }
                    maxHistory.Add(bestValue);
                    if (maxHistory.Count > 5)
                    {
                        Console.WriteLine("Restart");
                        break;
                    //    //var seed = Rnd.Next(1000);
                    //    //Console.WriteLine("New Seed: " + seed);
                    //    //Rnd = new Random(seed);
                    //    //maxHistory.Clear();
                    //    //maxHistory.Add(bestValue);
                    }
                }
            }
        }
    }

    private static Child ReadBestFromFile(string fileName)
    {
        var items = File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<Child>(items);
    }

    private static async Task Submit(MapData mapData, string[] names, Child best, double localScore)
    {
        SubmitSolution solution = new();
        for (var j = 0; j < best.F3100Counts.Length; j++)
        {
            if (best.F3100Counts[j] > 0 || best.F9100Counts[j] > 0)
            {
                solution.Locations[names[j]] = new PlacedLocations()
                {
                    Freestyle3100Count = best.F3100Counts[j],
                    Freestyle9100Count = best.F9100Counts[j]
                };
            }
        }

        await SolutionBase.SubmitSolution(mapData, localScore, solution);
    }

    private static Child[] EvaluateAndOrder(Child[] children, MapData mapData, GeneralData generalData)
    {
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();
        //for (int i = 0;i < children.Length;i++)
        Parallel.For(0, children.Length, (int i) =>
            {
                SubmitSolution solution = new();
                var child = children[i];
                for (var j = 0; j < children[i].F3100Counts.Length; j++)
                {
                    if (child.F3100Counts[j] > 0 || child.F9100Counts[j] > 0)
                    {
                        solution.Locations[names[j]] = new PlacedLocations()
                        {
                            Freestyle3100Count = child.F3100Counts[j],
                            Freestyle9100Count = child.F9100Counts[j]
                        };
                    }
                }

                var score = ScoringFaster.CalculateScore(solution, mapData, generalData);
                child.Score = score;

            }
        );
        return children.OrderByDescending(c => c.Score).ToArray();
    }

    private static Child RandomChild(int locationCount)
    {
        var child = new Child();
        child.F3100Counts = new int[locationCount];
        for (int j = 0; j < locationCount; j++)
            child.F3100Counts[j] = Rnd.Next(MaxStations);

        child.F9100Counts = new int[locationCount];
        for (int j = 0; j < locationCount; j++)
            child.F9100Counts[j] = Rnd.Next(MaxStations);

        return child;
    }

    private static void MakeChildren(Child[] children, int maleIndex, int femaleIndex)
    {
        var length = children[0].F9100Counts.Length;
        var male = new Child
        {
            F3100Counts = new int[length],
            F9100Counts = new int[length]
        };
        var female = new Child
        {
            F3100Counts = new int[length],
            F9100Counts = new int[length]
        };

        Array.Copy(children[maleIndex].F3100Counts, male.F3100Counts, length);
        Array.Copy(children[maleIndex].F9100Counts, male.F9100Counts, length);

        Array.Copy(children[femaleIndex].F3100Counts, female.F3100Counts, length);
        Array.Copy(children[femaleIndex].F9100Counts, female.F9100Counts, length);

        for (int i = 1; i < children.Length; i++)
        {
            // todo: try more genes
            var split = 0;
            do {
                var mLength = Rnd.Next(length / 4, length / 4 + 5);
                if (split + mLength >= length)
                    mLength = length - split;

                if (mLength == 0)
                    break;

                if (mLength % 2 == 0)
                {
                    Array.Copy(male.F3100Counts, split, children[i].F3100Counts, split, mLength);
                    Array.Copy(male.F9100Counts, split, children[i].F9100Counts, split, mLength);    
                } else {
                    Array.Copy(female.F3100Counts, split, children[i].F3100Counts, split, mLength);
                    Array.Copy(female.F9100Counts, split, children[i].F9100Counts, split, mLength);
                }
                split += mLength;
            } while(true);

            for (int x = 0; x < Mutations; x++)
            {
                children[i].F9100Counts[Rnd.Next(length)] = Rnd.Next(MaxStations + 1);
                children[i].F3100Counts[Rnd.Next(length)] = Rnd.Next(MaxStations + 1);    
            }            
        }
    }

    public class Child
    {
        public double Score { get; set; }
        public int[] F3100Counts { get; set; }
        public int[] F9100Counts { get; set; }

        internal Child Copy()
        {
            var child = new Child
            {
                Score = Score,
                F3100Counts = new int[F3100Counts.Length],
                F9100Counts = new int[F9100Counts.Length]
            };

            Array.Copy(F3100Counts, child.F3100Counts, F3100Counts.Length);
            Array.Copy(F9100Counts, child.F9100Counts, F9100Counts.Length);
            return child;
        }
    }
}