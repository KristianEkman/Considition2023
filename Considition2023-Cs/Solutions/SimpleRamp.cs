using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs.Solutions
{
    internal class SimpleRamp
    {
        public static async void Run(MapData data, GeneralData generalData)
        {
            (double Large, double Small, double Score, SubmitSolution Solution) max = (0, 0, 0d, new SubmitSolution());
            for (int i = 1; i < 200; i += 1)
            {
                for (int j = 0; j < 50; j += 1)
                {
                    var clone = JsonConvert.DeserializeObject<MapData>(JsonConvert.SerializeObject(data));
                    var solution = new SubmitSolution();
                    var score = Run(clone, generalData, i, j, solution);
                    if (score > max.Score)
                    {
                        max = (i, j, score, solution);
                    }
                }              
            }
            Console.WriteLine($"Max\t{max.Large}\t{max.Small}\t{max.Score.ToSI()}");

            HttpClient client = new();
            Api api = new(client);
            GameData prodScore = await api.SumbitAsync(data.MapName, max.Solution, HelperExtensions.Apikey);
            Console.WriteLine($"GameId: {prodScore.Id}");
            prodScore.GameScore.PrintJson();            
        }

        private static double Run(MapData mapData, GeneralData generalData, double largeLimit, double smallLimit, SubmitSolution solution)
        {
            foreach (KeyValuePair<string, StoreLocation> locationKeyPair in mapData.locations)
            {
                StoreLocation location = locationKeyPair.Value;                
                var salesVolume = location.SalesVolume;
                if (salesVolume > largeLimit)
                {
                    solution.Locations[location.LocationName] = new PlacedLocations()
                    {
                        Freestyle3100Count = 0,
                        Freestyle9100Count = 1
                    };
                } else if (salesVolume > smallLimit)
                {
                    solution.Locations[location.LocationName] = new PlacedLocations()
                    {
                        Freestyle3100Count = 1,
                        Freestyle9100Count = 0
                    };
                }
            }

            GameData score = new Scoring().CalculateScore(string.Empty, solution, mapData, generalData);
            // score.GameScore.PrintJson();

            Console.WriteLine($"{largeLimit}\t{smallLimit}\t{score.GameScore.Total.ToSI()}");
            return score.GameScore.Total;
        }
    }
}
