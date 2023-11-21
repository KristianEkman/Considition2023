using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs
{
    internal static class ScoringFaster
    {
        public static List<string> SandBoxMaps { get; } = new List<string> { "s-sandbox", "g-sandbox" };

        public static double CalculateScore(SubmitSolution solution, MapData mapData, GeneralData generalData, bool sandBox = false, bool distCache = true, bool rounding = false)
        {
            var scoreLoc = new Dictionary<int, StoreLocationScoring>();
            var KgCo2Savings = 0d;
            var TotalRevenue = 0d;
            var TotalFreestyle3100Count = 0d;
            var TotalFreestyle9100Count = 0d;
            var TotalFootfall = 0d;
            var TotalLeasingCost = 0d;
            
            if (!sandBox)
            {
                //Separate locations on the map into dict for those that have a refill station and those who have not.
                Dictionary<int, StoreLocationScoring> locationListNoRefillStation = new();
                //Dictionary<string, StoreLocationScoring> locationListWithRefillStation = new();
                foreach (KeyValuePair<string, StoreLocation> kvp in mapData.locations)
                {
                    var mapLoc = kvp.Value; 
                    if (solution.Locations.ContainsKey(kvp.Key))
                    {
                        var solLoc = solution.Locations[kvp.Key];
                        scoreLoc[mapLoc.IndexKey] = new()
                        {
                            LocationName = mapLoc.LocationName,
                            LocationType = mapLoc.LocationType,
                            Latitude = mapLoc.Latitude,
                            Longitude = mapLoc.Longitude,
                            Footfall = mapLoc.Footfall,
                            FootfallScale = mapLoc.footfallScale,
                            Freestyle3100Count = solLoc.Freestyle3100Count,
                            Freestyle9100Count = solLoc.Freestyle9100Count,

                            SalesVolume = mapLoc.SalesVolume,

                            SalesCapacity = solLoc.Freestyle3100Count * generalData.Freestyle3100Data.RefillCapacityPerWeek +
                                solLoc.Freestyle9100Count * generalData.Freestyle9100Data.RefillCapacityPerWeek,

                            LeasingCost = solLoc.Freestyle3100Count * generalData.Freestyle3100Data.LeasingCostPerWeek +
                                solLoc.Freestyle9100Count * generalData.Freestyle9100Data.LeasingCostPerWeek,
                            IndexKey = mapLoc.IndexKey
                        };

                        if (scoreLoc[mapLoc.IndexKey].SalesCapacity > 0 == false)
                        {
                            return 0;
                        }
                    }
                    else
                        locationListNoRefillStation[mapLoc.IndexKey] = new()
                        {
                            LocationName = mapLoc.LocationName,
                            LocationType = mapLoc.LocationType,
                            Latitude = mapLoc.Latitude,
                            Longitude = mapLoc.Longitude,
                            SalesVolume = mapLoc.SalesVolume,
                            IndexKey = mapLoc.IndexKey
                        };
                }


                //Throw an error if no valid locations with a refill station was found
                if (scoreLoc.Count == 0)
                    return 0;

                //Distribute sales from locations without a refill station to those with.
                scoreLoc = DistributeSales(scoreLoc, locationListNoRefillStation, generalData, distCache);
            }
            else
            {
                scoreLoc = InitiateSandboxLocations(scoreLoc, generalData, solution, distCache);
                scoreLoc = CalcualteFootfall(scoreLoc, mapData, distCache);
            }

            scoreLoc = DivideFootfall(scoreLoc, generalData, distCache);

            foreach (KeyValuePair<int, StoreLocationScoring> kvp in scoreLoc)
            {
                var loc = kvp.Value;
                if (rounding)
                    loc.SalesVolume = Math.Round(loc.SalesVolume, 0);
                if (loc.Footfall <= 0 && sandBox)
                {
                    loc.SalesVolume = 0;
                }
                double sales = loc.SalesVolume;
                if (loc.SalesCapacity < loc.SalesVolume) { sales = loc.SalesCapacity; }

                loc.GramCo2Savings = sales * (generalData.ClassicUnitData.Co2PerUnitInGrams - generalData.RefillUnitData.Co2PerUnitInGrams)
                    - loc.Freestyle3100Count * generalData.Freestyle3100Data.StaticCo2
                    - loc.Freestyle9100Count * generalData.Freestyle9100Data.StaticCo2;

                KgCo2Savings += loc.GramCo2Savings / 1000;
                
                loc.Revenue = sales * generalData.RefillUnitData.ProfitPerUnit;
                TotalRevenue += loc.Revenue;

                loc.Earnings = loc.Revenue - loc.LeasingCost;
                
                TotalLeasingCost += loc.LeasingCost;
                TotalFreestyle3100Count += loc.Freestyle3100Count;
                TotalFreestyle9100Count += loc.Freestyle9100Count;

                TotalFootfall += loc.Footfall / 1000;
            }

            //Just some rounding for nice whole numbers
            if (rounding)
            {
                TotalRevenue = Math.Round(TotalRevenue, 2);
                KgCo2Savings = Math.Round(KgCo2Savings, 2);
                TotalFootfall = Math.Round(TotalFootfall, 4);
            }

            //Calculate Earnings
            var earnings = (TotalRevenue - TotalLeasingCost) / 1000;

            //Calculate total score
            if (rounding)
                return Math.Round(
                (KgCo2Savings * generalData.Co2PricePerKiloInSek + earnings) *
                (1 + TotalFootfall), 2);
            return (KgCo2Savings * generalData.Co2PricePerKiloInSek + earnings) *
                (1 + TotalFootfall);
        }

        private static Dictionary<int, StoreLocationScoring> DistributeSales(Dictionary<int, StoreLocationScoring> with, Dictionary<int, StoreLocationScoring> without, GeneralData generalData, bool useCache)
        {
            foreach (KeyValuePair<int, StoreLocationScoring> kvpWithout in without)
            {
                Dictionary<int, double> distributeSalesTo = new();
                //double locationSalesFrom = await GetSalesVolume(kvpWithout.Value.LocationType) ?? throw new Exception(string.Format("Location: {0}, have an invalid location type: {1}", kvpWithout.Key, kvpWithout.Value.LocationType));

                foreach (KeyValuePair<int, StoreLocationScoring> kvpWith in with)
                {
                    int distance = kvpWithout.Value.DistanceBetweenPoint(kvpWith.Value, useCache);
                    if (distance < generalData.WillingnessToTravelInMeters)
                    {
                        distributeSalesTo[kvpWith.Value.IndexKey] = distance;
                    }
                }

                double total = 0;
                if (distributeSalesTo.Count > 0)
                {
                    foreach (KeyValuePair<int, double> kvp in distributeSalesTo)
                    {
                        distributeSalesTo[kvp.Key] = Math.Pow(generalData.ConstantExpDistributionFunction, generalData.WillingnessToTravelInMeters - kvp.Value) - 1;
                        total += distributeSalesTo[kvp.Key];
                    }

                    //Add boosted sales to original sales volume
                    foreach (KeyValuePair<int, double> kvp in distributeSalesTo)
                    {
                        with[kvp.Key].SalesVolume += distributeSalesTo[kvp.Key] / total *
                        generalData.RefillDistributionRate * kvpWithout.Value.SalesVolume;//locationSalesFrom;
                    }
                }
            }

            return with;
        }

        public static Dictionary<int, StoreLocationScoring> CalcualteFootfall(Dictionary<int, StoreLocationScoring> locations, MapData mapEntity, bool useCache)
        {
            double maxFootfall = 0;
            foreach (KeyValuePair<int, StoreLocationScoring> kvpLoc in locations)
            {
                foreach (Hotspot hotspot in mapEntity.Hotspots)
                {
                    double distanceInMeters = hotspot.DistanceBetweenPoint(kvpLoc.Value, useCache);
                        
                    double maxSpread = hotspot.Spread;
                    if (distanceInMeters <= maxSpread)
                    {
                        double val = hotspot.Footfall * (1 - (distanceInMeters / maxSpread));
                        kvpLoc.Value.Footfall += val / 10;
                    }
                }
                if (maxFootfall < kvpLoc.Value.Footfall)
                {
                    maxFootfall = kvpLoc.Value.Footfall;
                }
            }
            if (maxFootfall > 0)
            {
                foreach (KeyValuePair<int, StoreLocationScoring> kvpLoc in locations)
                {
                    if (kvpLoc.Value.Footfall > 0)
                    {
                        kvpLoc.Value.FootfallScale = Convert.ToInt32(kvpLoc.Value.Footfall / maxFootfall * 10);
                        if (kvpLoc.Value.FootfallScale == 0)
                        {
                            kvpLoc.Value.FootfallScale = 1;
                        }
                    }
                }
            }
            return locations;
        }
        private static double GetSalesVolume(string locationType, GeneralData generalData)
        {
            foreach (KeyValuePair<string, LocationType> kvpLoc in generalData.LocationTypes)
            {
                if (locationType == kvpLoc.Value.Type)
                {
                    return kvpLoc.Value.SalesVolume;
                }
            }
            return 0;
        }
        public static Dictionary<int, StoreLocationScoring> InitiateSandboxLocations(Dictionary<int, StoreLocationScoring> locations, GeneralData generalData, SubmitSolution request, bool useCache)
        {
            foreach (KeyValuePair<string, PlacedLocations> kvpLoc in request.Locations)
            {
                double sv = GetSalesVolume(kvpLoc.Value.LocationType, generalData);
                StoreLocationScoring scoredSolution = new()
                {
                    Longitude = kvpLoc.Value.Longitude,
                    Latitude = kvpLoc.Value.Latitude,
                    Freestyle3100Count = kvpLoc.Value.Freestyle3100Count,
                    Freestyle9100Count = kvpLoc.Value.Freestyle9100Count,
                    LocationType = kvpLoc.Value.LocationType,
                    LocationName = kvpLoc.Key,
                    SalesVolume = sv,
                    SalesCapacity = request.Locations[kvpLoc.Key].Freestyle3100Count * generalData.Freestyle3100Data.RefillCapacityPerWeek +
                                request.Locations[kvpLoc.Key].Freestyle9100Count * generalData.Freestyle9100Data.RefillCapacityPerWeek,
                    LeasingCost = request.Locations[kvpLoc.Key].Freestyle3100Count * generalData.Freestyle3100Data.LeasingCostPerWeek +
                                request.Locations[kvpLoc.Key].Freestyle9100Count * generalData.Freestyle9100Data.LeasingCostPerWeek,
                    IndexKey = kvpLoc.Value.IndexKey

                };
                locations.Add(kvpLoc.Value.IndexKey, scoredSolution);
            }
            foreach (KeyValuePair<int, StoreLocationScoring> kvpScope in locations)
            {
                int count = 1;
                //Dictionary<string, double> distributeSalesTo = new();
                foreach (KeyValuePair<int, StoreLocationScoring> kvpSurrounding in locations)
                {
                    if (kvpScope.Key != kvpSurrounding.Key)
                    {
                        int distance = kvpScope.Value.DistanceBetweenPoint(kvpSurrounding.Value, useCache);
                            
                        if (distance < generalData.WillingnessToTravelInMeters)
                        {
                            count++;
                        }
                    }
                }

                kvpScope.Value.SalesVolume = kvpScope.Value.SalesVolume / count;

            }
            return locations;
        }

        public static Dictionary<int, StoreLocationScoring> DivideFootfall(Dictionary<int, StoreLocationScoring> locations, GeneralData generalData, bool useCache)
        {
            foreach (KeyValuePair<int, StoreLocationScoring> kvpScope in locations)
            {
                int count = 1;
                foreach (KeyValuePair<int, StoreLocationScoring> kvpSurrounding in locations)
                {
                    if (kvpScope.Key != kvpSurrounding.Key)
                    {
                        int distance = kvpScope.Value.DistanceBetweenPoint(kvpSurrounding.Value, useCache);
                            
                        if (distance < generalData.WillingnessToTravelInMeters)
                        {
                            count++;
                        }
                    }
                }

                kvpScope.Value.Footfall = kvpScope.Value.Footfall / count;

            }
            return locations;
        }

        internal const int maxGroceryStoreLarge = 5;
        internal const int maxGroceryStore = 20;
        internal const int maxConvenience = 20;
        internal const int maxGasStation = 8;
        internal const int maxKiosk = 3;
        internal const int totalStores = maxGroceryStoreLarge + maxGroceryStore + maxConvenience + maxGasStation + maxKiosk;

        public static string SandboxValidation(string inMapName, SubmitSolution request, MapData mapData)
        {
            int countGroceryStoreLarge = 0;
            int countGroceryStore = 0;
            int countConvenience = 0;
            int countGasStation = 0;
            int countKiosk = 0;
            
            string numberErrorMsg = string.Format("locationName needs to start with 'location' and followed with a number larger than 0 and less than {0}.", totalStores + 1);
            string mapName = inMapName.ToLower();
            foreach (KeyValuePair<string, PlacedLocations> kvp in request.Locations)
            {
                //Validate location name
                if (kvp.Key.StartsWith("location") == false)
                {
                    return string.Format("{0} {1} is not a valid name", numberErrorMsg, kvp.Key);
                }
                string loc_num = kvp.Key.Substring(8);
                if (string.IsNullOrWhiteSpace(loc_num))
                {

                    return string.Format("{0} Nothing followed location in the locationName", numberErrorMsg);
                }
                var isNumeric = int.TryParse(loc_num, out int n);
                if (isNumeric == false)
                {
                    return string.Format("{0} {1} is not a number", numberErrorMsg, loc_num);
                }
                if (n <= 0 || n > totalStores)
                {
                    return string.Format("{0} {1} is not within the constraints", numberErrorMsg, n);
                }
                //Validate long and lat
                if (mapData.Border.LatitudeMin > kvp.Value.Latitude || mapData.Border.LatitudeMax < kvp.Value.Latitude)
                {
                    return  string.Format("Latitude is missing or out of bounds for location : {0}", kvp.Key);
                }
                if (mapData.Border.LongitudeMin > kvp.Value.Longitude || mapData.Border.LongitudeMax < kvp.Value.Longitude)
                {
                    return string.Format("Longitude is missing or out of bounds for location : {0}", kvp.Key);
                }
                //Validate locationType
                if (kvp.Value.LocationType.Equals(string.Empty))
                {
                    return string.Format("locationType is missing for location) : {0}", kvp.Key);
                }
                else if (kvp.Value.LocationType.Equals("Grocery-store-large"))
                {
                    countGroceryStoreLarge += 1;
                }
                else if (kvp.Value.LocationType.Equals("Grocery-store"))
                {
                    countGroceryStore += 1;
                }
                else if (kvp.Value.LocationType.Equals("Convenience"))
                {
                    countConvenience += 1;
                }
                else if (kvp.Value.LocationType.Equals("Gas-station"))
                {
                    countGasStation += 1;
                }
                else if (kvp.Value.LocationType.Equals("Kiosk"))
                {
                    countKiosk += 1;
                }
                else
                {
                    return string.Format("locationType --> {0} not valid (check GetGeneralGameData for correct values) for location : {1}", kvp.Value.LocationType, kvp.Key);
                }
                //Validate that max number of location is not exceeded
                if (countGroceryStoreLarge > maxGroceryStoreLarge || countGroceryStore > maxGroceryStore ||
                    countConvenience > maxConvenience || countGasStation > maxGasStation ||
                    countKiosk > maxKiosk)
                {
                    return string.Format("Number of allowed locations exceeded for locationType: {0}", kvp.Value.LocationType);
                }
            }
            return null;
        }

        private static int DistanceBetweenPoint(this StoreLocationScoring location1, StoreLocationScoring location2, bool useCache)
        {
            if (useCache)
            {
                if (DistanceCache.Values[location1.IndexKey][location2.IndexKey] != 0)
                    return DistanceCache.Values[location1.IndexKey][location2.IndexKey];
                if (DistanceCache.Values[location2.IndexKey][location1.IndexKey] != 0)
                    return DistanceCache.Values[location2.IndexKey][location1.IndexKey];
            }

            double latitude1 = location1.Latitude;
            double longitude1 = location1.Longitude;
            double latitude2 = location2.Latitude;
            double longitude2 = location2.Longitude;

            double r = 6371e3;
            double latRadian1 = latitude1 * Math.PI / 180;
            double latRadian2 = latitude2 * Math.PI / 180;

            double latDelta = (latitude2 - latitude1) * Math.PI / 180;
            double longDelta = (longitude2 - longitude1) * Math.PI / 180;

            double a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                Math.Cos(latRadian1) * Math.Cos(latRadian2) *
                Math.Sin(longDelta / 2) * Math.Sin(longDelta / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            int distance = (int)Math.Round(r * c, 0);

            if (useCache)
            {
                DistanceCache.Values[location1.IndexKey][location2.IndexKey] = distance;
                DistanceCache.Values[location2.IndexKey][location1.IndexKey] = distance;
            }

            return distance;
        }

        private static int DistanceBetweenPoint(this Hotspot location1, StoreLocationScoring location2, bool useCache)
        {
            if (useCache)
            {
                if (DistanceCache.Values[location1.IndexKey][location2.IndexKey] != 0)
                    return DistanceCache.Values[location1.IndexKey][location2.IndexKey];
                if (DistanceCache.Values[location2.IndexKey][location1.IndexKey] != 0)
                    return DistanceCache.Values[location2.IndexKey][location1.IndexKey];
            }

            double latitude1 = location1.Latitude;
            double longitude1 = location1.Longitude;
            double latitude2 = location2.Latitude;
            double longitude2 = location2.Longitude;

            double r = 6371e3;
            double latRadian1 = latitude1 * Math.PI / 180;
            double latRadian2 = latitude2 * Math.PI / 180;

            double latDelta = (latitude2 - latitude1) * Math.PI / 180;
            double longDelta = (longitude2 - longitude1) * Math.PI / 180;

            double a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                Math.Cos(latRadian1) * Math.Cos(latRadian2) *
                Math.Sin(longDelta / 2) * Math.Sin(longDelta / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            int distance = (int)Math.Round(r * c, 0);

            if (useCache)
            {
                DistanceCache.Values[location1.IndexKey][location2.IndexKey] = distance;
                DistanceCache.Values[location2.IndexKey][location1.IndexKey] = distance;
            }

            return distance;
        }
    }
}
