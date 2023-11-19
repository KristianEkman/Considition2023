using Considition2023_Cs;

const string apikey = "20d51f18-3a6f-4419-8466-1fac81f7e540";
var mapName = "";
Parse(args);

if (string.IsNullOrEmpty(mapName))
     mapName = SelectMap();

if (string.IsNullOrEmpty(mapName))
{
    Console.WriteLine("Invalid map selected");
    return;
}

bool isSandBox = Scoring.SandBoxMaps.Contains(mapName.ToLower());

HttpClient client = new();
Api api = new(client);
MapData mapData = await api.GetMapDataAsync(mapName, apikey);
GeneralData generalData = await api.GetGeneralDataAsync();

if (isSandBox)
    SandboxSearch.Run(mapData, generalData, true);
else
    GeneticSearchFaster.Run(mapData, generalData, true);

void Parse(string[] args)
{
    foreach (var arg in args)
    {
        var split = arg.Split("=");
        if (split.Length != 2) continue;

        switch (split[0])
        {
            case "ChildCount":
                GeneticSearch.ChildCount = int.Parse(split[1]);
                GeneticSearchFaster.ChildCount = int.Parse(split[1]);
                SandboxSearch.ChildCount = int.Parse(split[1]);
                Console.WriteLine($"ChildCount={split[1]}");
                break;
            case "Mutations":
                GeneticSearch.Mutations = int.Parse(split[1]);
                GeneticSearchFaster.Mutations = int.Parse(split[1]);
                SandboxSearch.Mutations = int.Parse(split[1]);
                Console.WriteLine($"Mutations={split[1]}");
                break;
            case "Rounding":
                GeneticSearch.Rounding = split[1] == "true";
                GeneticSearchFaster.Rounding = split[1] == "true"; ;
                SandboxSearch.Rounding = split[1] == "true"; ;
                Console.WriteLine($"Rounding={split[1]}");
                break;
            case "Map":
                Console.WriteLine($"Map={split[1]}");
                mapName = split[1];
                break;
        }
    }
}

static string SelectMap()
{
    Console.WriteLine($"1: {MapNames.Stockholm}");
    Console.WriteLine($"2: {MapNames.Goteborg}");
    Console.WriteLine($"3: {MapNames.Malmo}");
    Console.WriteLine($"4: {MapNames.Uppsala}");
    Console.WriteLine($"5: {MapNames.Vasteras}");
    Console.WriteLine($"6: {MapNames.Orebro}");
    Console.WriteLine($"7: {MapNames.London}");
    Console.WriteLine($"8: {MapNames.Linkoping}");
    Console.WriteLine($"9: {MapNames.Berlin}");
    Console.WriteLine($"10: {MapNames.GSandbox}");
    Console.WriteLine($"11: {MapNames.SSandbox}");

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
        "10" => MapNames.GSandbox,
        "11" => MapNames.SSandbox,
        _ => null
    };
    return mapName;
}