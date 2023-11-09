using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal class Scoring
    {
        public static GameData CalculateScore(string mapName, SubmitSolution solution, MapData mapEntity, GeneralData generalData)
        {
            GameData scored = new() 
            {
                MapName = mapName,
                TeamId = Guid.Empty,
                TeamName = string.Empty,
                Locations = new(),
                GameScore = new()
            };
            Dictionary<string, StoreLocationScoring> locationListNoRefillStation = new();
            foreach (KeyValuePair<string, StoreLocation> kvp in mapEntity.locations)
            {
                if (solution.Locations.ContainsKey(kvp.Key) == true)
                {
                    scored.Locations[kvp.Key] = new()
                    {
                        IndexKey = kvp.Value.IndexKey,
                        LocationName = kvp.Value.LocationName,
                        LocationType = kvp.Value.LocationType,
                        Latitude = kvp.Value.Latitude,
                        Longitude = kvp.Value.Longitude,
                        Footfall = kvp.Value.Footfall,
                        Freestyle3100Count = solution.Locations[kvp.Key].Freestyle3100Count,
                        Freestyle9100Count = solution.Locations[kvp.Key].Freestyle9100Count,

                        SalesVolume = kvp.Value.SalesVolume * generalData.RefillSalesFactor,
                        // await GetSalesVolume(kvp.Value.LocationType) ??
                        //     throw new Exception(string.Format("Location: {0}, have an invalid location type: {1}", kvp.Key, kvp.Value.LocationType)),

                        SalesCapacity = solution.Locations[kvp.Key].Freestyle3100Count * generalData.Freestyle3100Data.RefillCapacityPerWeek +
                            solution.Locations[kvp.Key].Freestyle9100Count * generalData.Freestyle9100Data.RefillCapacityPerWeek,

                        LeasingCost = solution.Locations[kvp.Key].Freestyle3100Count * generalData.Freestyle3100Data.LeasingCostPerWeek +
                            solution.Locations[kvp.Key].Freestyle9100Count * generalData.Freestyle9100Data.LeasingCostPerWeek
                    };

                    if (scored.Locations[kvp.Key].SalesCapacity > 0 == false)
                    {
                        throw new Exception(string.Format("You are not allowed to submit locations with no refill stations. Remove or alter location : {0}", kvp.Value.LocationName));
                    }
                }
                else
                    locationListNoRefillStation[kvp.Key] = new()
                    {
                        LocationName = kvp.Value.LocationName,
                        LocationType = kvp.Value.LocationType,
                        Latitude = kvp.Value.Latitude,
                        Longitude = kvp.Value.Longitude,
                        SalesVolume = kvp.Value.SalesVolume * generalData.RefillSalesFactor,
                        //await GetSalesVolume(kvp.Value.LocationType) ?? throw new Exception(string.Format("Location: {0}, have an invalid location type: {1}", kvp.Key, kvp.Value.LocationType)),
                    };
            }

            if (scored.Locations.Count == 0)
                throw new Exception(string.Format("No valid locations with refill stations were placed for map: {0}", mapName));
            scored.Locations = DistributeSales(scored.Locations, locationListNoRefillStation, generalData);

            foreach (KeyValuePair<string, StoreLocationScoring> kvp in scored.Locations)
            {
                kvp.Value.SalesVolume = Math.Round(kvp.Value.SalesVolume, 0);

                double sales = kvp.Value.SalesVolume;
                if (kvp.Value.SalesCapacity < kvp.Value.SalesVolume) { sales = kvp.Value.SalesCapacity; }

                kvp.Value.GramCo2Savings = sales * (generalData.ClassicUnitData.Co2PerUnitInGrams - generalData.RefillUnitData.Co2PerUnitInGrams);
                scored.GameScore.KgCo2Savings += kvp.Value.GramCo2Savings / 1000; //Kristian: * 1000 (eller?)
                if (kvp.Value.GramCo2Savings > 0)
                {
                    kvp.Value.IsCo2Saving = true;
                }

                kvp.Value.Revenue = sales * generalData.RefillUnitData.ProfitPerUnit;
                scored.TotalRevenue += kvp.Value.Revenue;

                kvp.Value.Earnings = kvp.Value.Revenue - kvp.Value.LeasingCost;
                if (kvp.Value.Earnings > 0)
                {
                    kvp.Value.IsProfitable = true;
                }

                scored.TotalLeasingCost += kvp.Value.LeasingCost;

                scored.TotalFreestyle3100Count += kvp.Value.Freestyle3100Count;
                scored.TotalFreestyle9100Count += kvp.Value.Freestyle9100Count;

                scored.GameScore.TotalFootfall += kvp.Value.Footfall;


            }

            //Just some rounding for nice whole numbers
            scored.TotalRevenue = Math.Round(scored.TotalRevenue, 0);
            scored.GameScore.KgCo2Savings = Math.Round(
                scored.GameScore.KgCo2Savings
                - scored.TotalFreestyle3100Count * generalData.Freestyle3100Data.StaticCo2 / 1000
                - scored.TotalFreestyle9100Count * generalData.Freestyle9100Data.StaticCo2 / 1000
                , 0);

            //Calculate Earnings
            scored.GameScore.Earnings = scored.TotalRevenue - scored.TotalLeasingCost;

            //Calculate total score
            scored.GameScore.Total = Math.Round(
                (scored.GameScore.KgCo2Savings * generalData.Co2PricePerKiloInSek + scored.GameScore.Earnings) *
                (1 + scored.GameScore.TotalFootfall),
                0
            );

            return scored;
        }

        private static Dictionary<string, StoreLocationScoring> DistributeSales(Dictionary<string, StoreLocationScoring> with, Dictionary<string, StoreLocationScoring> without, GeneralData generalData)
        {
            foreach (KeyValuePair<string, StoreLocationScoring> kvpWithout in without)
            {
                // Lista av distanser till platser som har station och som ligger tillräckligt nära.
                Dictionary<string, double> distributeSalesTo = new();
                
                foreach (KeyValuePair<string, StoreLocationScoring> kvpWith in with)
                {
                    int distance = kvpWithout.Value.DistanceTo(kvpWith.Value);
                    //DistanceBetweenPoint(kvpWithout.Value.Latitude, kvpWithout.Value.Longitude, kvpWith.Value.Latitude, kvpWith.Value.Longitude                    
                    if (distance < generalData.WillingnessToTravelInMeters)
                    {
                        distributeSalesTo[kvpWith.Value.LocationName] = distance;
                    }
                }

                double total = 0;
                // Försäljningsvolymen ökas litegrann för de toma platserna i närheten.
                if (distributeSalesTo.Count > 0)
                {
                    foreach (KeyValuePair<string, double> kvp in distributeSalesTo)
                    {
                        distributeSalesTo[kvp.Key] = Math.Pow(generalData.ConstantExpDistributionFunction, generalData.WillingnessToTravelInMeters - kvp.Value) - 1;
                        total += distributeSalesTo[kvp.Key];
                    }

                    //Add boosted sales to original sales volume
                    foreach (KeyValuePair<string, double> kvp in distributeSalesTo)
                    {
                        with[kvp.Key].SalesVolume += distributeSalesTo[kvp.Key] / total *
                        generalData.RefillDistributionRate * kvpWithout.Value.SalesVolume;
                    }
                }
            }

            return with;
        }
            
        public static int[][] Distances {  get; set; } = new int[1000][];

        internal static void NewDistancesCache()
        {
            for (int i = 0; i < Distances.Length; i++)
                Distances[i] = new int[1000];
        }
    }
}
