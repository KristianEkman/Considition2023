using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Considition2023_Cs.Solutions
{
    internal static class SolutionBase
    {
        internal static async Task SubmitSolutionAsync(MapData mapData, double localScore, SubmitSolution solution)
        {
            return;
            HttpClient client = new();
            Api api = new(client);
            var prodScore = await api.SumbitAsync(mapData.MapName, solution, HelperExtensions.Apikey);
            var result = $"\r\n{mapData.MapName}: {prodScore.Id} {prodScore.GameScore.Total:0.##} at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
            Console.WriteLine(result);
            File.AppendAllText("resultslog.txt", result);

            if (localScore != prodScore.GameScore.Total)
                Console.WriteLine($"DIFF!!: {(localScore - prodScore.GameScore.Total) / (double)prodScore.GameScore.Total:P}");
        }
    }
}
