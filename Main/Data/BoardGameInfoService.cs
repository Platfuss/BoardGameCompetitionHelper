using Main.Data.DTO;
using Main.Data.Helper;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static Main.Data.DTO.BggResponse;

namespace Main.Data;

public class BoardGameInfoService
{
    private static readonly Regex _exTrash = new(@"[\-':()!?+/\\]", RegexOptions.Compiled);
    private static readonly Regex _exNormalLetters = new(@"[a-zA-Z]", RegexOptions.Compiled);

    private readonly string _outputPath;

    public BoardGameInfoService(IWebHostEnvironment hostEnvironment)
    {
        _outputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames.json");
    }

    internal async Task FetchDataAsync()
    {
        HttpClient client = new() { BaseAddress = new Uri("https://boardgamegeek.com/xmlapi/boardgame/") };
        Boardgames bggResponse = new();
        List<BoardGameDump> dumps = new();
        int index = 1, step = 300;

        do
        {
            HttpResponseMessage response = await client.GetAsync(string.Join(',', Enumerable.Range(index, step)));
            index += step;

            bggResponse = XmlReader<Boardgames>.Deserialize(response.Content.ReadAsStringAsync().Result);
            dumps.AddRange(bggResponse.Boardgame?.Select(b => CreateDump(b)).Where(d => d != null).ToArray()!);

            Thread.Sleep(5_500);
        } while (bggResponse?.Boardgame?.Count >= 2);

        File.WriteAllText(_outputPath, JsonConvert.SerializeObject(dumps));
    }

    private static BoardGameDump? CreateDump(Boardgame boardGame)
    {
        if (boardGame == null)
            return null;

        BoardGameDump dump = new(int.Parse(boardGame.Objectid), boardGame.Name.Single(n => n.Primary == "true").Text);

        List<string> names = boardGame?.Name?.Select(n => _exTrash.Replace(n.Text, string.Empty).RemoveDiacritics()).Distinct().ToList() ?? new();
        string[] partialNames = names.SelectMany(n => n.Split(' ').Take(2).Where(w => w.Length > 3)).ToArray();
        names.AddRange(partialNames);

        dump.Names = names.Where(n => _exNormalLetters.IsMatch(n)).Select(n => n.Replace(" ", string.Empty)).Distinct().ToList();
        return dump;
    }
}
