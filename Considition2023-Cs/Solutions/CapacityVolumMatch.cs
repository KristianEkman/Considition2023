using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs.Solutions
{
    internal class CapacityVolumeMatch
    {
        public static void Run(MapData mapData, GeneralData generalData)
        {
            SubmitSolution solution = new()
            {
                Locations = new()
            };
            foreach (KeyValuePair<string, StoreLocation> locationKeyPair in mapData.locations)
            {
                StoreLocation location = locationKeyPair.Value;
                //string name = locationKeyPair.Key;
                var salesVolume = location.SalesVolume * generalData.RefillSalesFactor;
                var f3100Count = 0;
                var f9100Count = 0;

                // TODO: a smarter way to match
                while (salesVolume > generalData.Freestyle9100Data.RefillCapacityPerWeek)
                {
                    f9100Count++;
                    salesVolume -= generalData.Freestyle9100Data.RefillCapacityPerWeek;
                }

                while (salesVolume > generalData.Freestyle3100Data.RefillCapacityPerWeek)
                {
                    f3100Count++;
                    salesVolume -= generalData.Freestyle3100Data.RefillCapacityPerWeek;
                }

                if (f3100Count > 5) { f3100Count = 5; }
                if (f9100Count > 5) { f9100Count = 5; }

                if (f3100Count > 0 || f9100Count > 0) {
                    solution.Locations[location.LocationName] = new PlacedLocations()
                    {
                        Freestyle3100Count = f3100Count,
                        Freestyle9100Count = f9100Count
                    };
                }
                
            }

            GameData score = Scoring.CalculateScore(mapData.MapName, solution, mapData, generalData);
            score.GameScore.PrintJson();
            Console.WriteLine($"GameScore: {score.GameScore.Total.ToSI()}");
        }
    }
}
