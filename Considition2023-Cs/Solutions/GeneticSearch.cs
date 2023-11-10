using Considition2023_Cs;

public class GeneticSearch
{
    static Random Rnd = new(777);    
    const int MaxRnd = 4;
    const int childCount = 500;
    const int Runs = 1000;
    const int Mutations =  2;

    public static async void Run(MapData mapData, GeneralData generalData)
    {
        var size = mapData.locations.Count;
        var male = RandomArray(size);
        var female = RandomArray(size);
        var children = new (int, int)[childCount][];
        var names = mapData.locations.Select(x => x.Value.LocationName).ToArray();

        for (int i = 0; i < childCount; i++)
        {
            children[i] = new (int, int)[size];
        }

        var max = 0d;
        (int Index, double Score)[] twoBest;
        var best = new (int, int)[size];
        for (int n = 0; n < Runs; n++)
        {
            MakeChildren(children, male, female);
            twoBest = Evaluate(children, mapData, generalData);
            male = children[twoBest[0].Index];
            female = children[twoBest[1].Index];

            if (twoBest[0].Score > max)
            {
                max = twoBest[0].Score;
                Array.Copy(children[twoBest[0].Index], best, best.Length);
            }

            if (n % 100 == 0)
            {
                Console.WriteLine($"{n}. {max.ToSI()}");
                await Submit(mapData, names, best.Clone() as (int, int)[]);                
            }
        }

        await Submit(mapData, names, best);
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
        Console.WriteLine($"GameId: {prodScore.Id} {prodScore.GameScore.Total}");
    }

    private static (int Index , double Score)[] Evaluate((int, int)[][] children, MapData mapData, GeneralData generalData)
    {
        var topList = new List<(int Index, double Score)>();
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
            lock(topList)
            {
                topList.Add((i, score.GameScore.Total));
            }
        }
        );

        topList = topList.OrderByDescending(x => x.Score).ToList();        

        return topList.Take(2).ToArray();
    }

    private static (int, int)[] RandomArray(int size)
    {

        (int, int)[] a = new (int, int)[size];
        for (int i = 0; i < size; i++)
        {
            a[i] = (Rnd.Next(MaxRnd), Rnd.Next(MaxRnd));
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
                children[i][mutation] = (Rnd.Next(MaxRnd), Rnd.Next(MaxRnd));
            }
        }
    }
}