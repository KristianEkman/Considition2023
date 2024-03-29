﻿using Newtonsoft.Json;
using System.Text.Json;

namespace Considition2023_Cs;

internal class Api
{
    private readonly HttpClient _httpClient;

    public Api(HttpClient httpClient)
    {
        _httpClient = httpClient;
        httpClient.BaseAddress = new Uri("https://api.considition.com/");
    }

    public async Task<MapData> GetMapDataAsync(string mapName, string apiKey)
    {
        var fileName = $"{mapName}.mapdata.json";
        var fileText = fileName.ReadFileText();
        if (fileText != string.Empty)
            return JsonConvert.DeserializeObject<MapData>(fileText);

        HttpRequestMessage request = new();
        request.Method = HttpMethod.Get;
        request.RequestUri = new Uri($"/api/game/getmapdata?mapName={Uri.EscapeDataString(mapName)}", UriKind.Relative);
        request.Headers.Add("x-api-key", apiKey);
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        string responseText = await response.Content.ReadAsStringAsync();
        responseText.SaveTo(fileName);
        return JsonConvert.DeserializeObject<MapData>(responseText);
    }

    public async Task<GeneralData> GetGeneralDataAsync()
    {
        var fileName = $"general.data.json";
        var fileText = fileName.ReadFileText();
        if (fileText != string.Empty)
        {
            var gdata = JsonConvert.DeserializeObject<GeneralData>(fileText);
            HelperExtensions.LocationTypes = gdata.LocationTypes.ToArray();
            return gdata;
        }
        var response = await _httpClient.GetAsync("/api/game/getgeneralgamedata");
        response.EnsureSuccessStatusCode();
        string responseText = await response.Content.ReadAsStringAsync();
        responseText.SaveTo(fileName);
        var data = JsonConvert.DeserializeObject<GeneralData>(responseText);
        HelperExtensions.LocationTypes = data.LocationTypes.ToArray();
        return data;
    }

    public async Task<GameData> GetGameAsync(Guid id)
    {
        var response = await _httpClient.GetAsync($"/api/game/getgamedata{id}");
        response.EnsureSuccessStatusCode();
        string responseText = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<GameData>(responseText);
    }

    public async Task<GameData> SumbitAsync(string mapName, SubmitSolution solution, string apiKey)
    {
        HttpRequestMessage request = new();
        request.Method = HttpMethod.Post;
        request.RequestUri = new Uri($"/api/Game/submitSolution?mapName={Uri.EscapeDataString(mapName)}", UriKind.Relative);
        request.Headers.Add("x-api-key", apiKey);
        request.Content = new StringContent(JsonConvert.SerializeObject(solution), System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = _httpClient.Send(request);
        string responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(responseText);
        }
        response.EnsureSuccessStatusCode();
        return JsonConvert.DeserializeObject<GameData>(responseText);
    }
}
