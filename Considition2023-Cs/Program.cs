using Considition2023_Cs;
using Considition2023_Cs.Solutions;
using KristianEkman.GraphLib;
using System.Diagnostics;
using System.Text.Json.Serialization;

if (string.IsNullOrWhiteSpace(HelperExtensions.Apikey))
{
    Console.WriteLine("Configure apiKey");
    return;
}

//Console.WriteLine($"1: {MapNames.Stockholm}");
Console.WriteLine($"2: {MapNames.Goteborg}");
//Console.WriteLine($"3: {MapNames.Malmo}");
Console.WriteLine($"4: {MapNames.Uppsala}");
Console.WriteLine($"5: {MapNames.Vasteras}");
//Console.WriteLine($"6: {MapNames.Orebro}");
//Console.WriteLine($"7: {MapNames.London}");
Console.WriteLine($"8: {MapNames.Linkoping}");
//Console.WriteLine($"9: {MapNames.Berlin}");

Console.Write("Select the map you wish to play: ");
string option = Console.ReadLine();

var mapName = option switch
{
    "1" => MapNames.Stockholm,
    "2" => MapNames.Goteborg,
    "3" => MapNames.Malmo,
    "4" => MapNames.Uppsala,
    "5" => MapNames.Vasteras,
    "6" => MapNames.Orebro,
    "7" => MapNames.London,
    "8" => MapNames.Linkoping,
    "9" => MapNames.Berlin,
    _ => null
};

if (mapName is null)
{
    Console.WriteLine("Invalid map selected");
    return;
}

HttpClient client = new();
Api api = new(client);
MapData mapData = await api.GetMapDataAsync(mapName, HelperExtensions.Apikey);
//mapData.PrintJson();
GeneralData generalData = await api.GetGeneralDataAsync();
//generalData.PrintJson();

//OriginalExample.Run(mapData, generalData);
//SimpleRamp.Run(mapData, generalData);
//CapacityVolumeMatch.Run(mapData, generalData);
Stopwatch stopwatch = Stopwatch.StartNew();
GeneticSearch.Run(mapData, generalData);
Console.WriteLine("Took: " + stopwatch.ElapsedMilliseconds / 1000d);

//var graph = new Graph("Test.dgrm", new[] { "S1", "S2", "S3" });
//graph.Series[0].AddPoints((0, 0), (1,3), (5, 4));
//graph.Series[1].AddPoints((1, 1), (3,3), (7, 4));
//graph.Series[2].AddPoints((3, 2), (3,3), (8, 4));
//graph.Save("Test.dgrm");

// Console.ReadLine();
