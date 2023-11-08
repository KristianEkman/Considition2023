using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs.Solutions
{
    internal class OriginalExample
    {
        public static void Run(MapData mapData, GeneralData generalData)
        {
            SubmitSolution solution = new();
            foreach (KeyValuePair<string, StoreLocation> locationKeyPair in mapData.locations)
            {
                StoreLocation location = locationKeyPair.Value;
                //string name = locationKeyPair.Key;
                var salesVolume = location.SalesVolume;
                if (salesVolume > 100)
                {
                    solution.Locations[location.LocationName] = new PlacedLocations()
                    {
                        Freestyle3100Count = 0,
                        Freestyle9100Count = 1
                    };
                }
            }

            GameData score = Scoring.CalculateScore(string.Empty, solution, mapData, generalData);
            score.GameScore.PrintJson();
            Console.WriteLine($"GameScore: {score.GameScore.Total.ToSI()}");
        }
    }
}
