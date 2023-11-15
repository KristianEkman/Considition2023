﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs.Solutions
{
    internal static class SolutionBase
    {
        internal static async Task SubmitSolution(MapData mapData, double localScore, SubmitSolution solution)
        {
            if (localScore.IsToGood(mapData.MapName)) return;

            HttpClient client = new();
            Api api = new(client);
            GameData prodScore = await api.SumbitAsync(mapData.MapName, solution, HelperExtensions.Apikey);
            var result = $"\r\n{mapData.MapName}: {prodScore.Id} {prodScore.GameScore.Total.ToSI()} at {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")}";
            Console.WriteLine(result);
            File.AppendAllText("resultslog.txt", result);

            if (localScore != prodScore.GameScore.Total)
                Console.WriteLine($"DIFF!!:{localScore - prodScore.GameScore.Total}");
        }
    }
}