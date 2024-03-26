using Main.Data.DTO;
using Main.Data.Helper;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static Main.Data.DTO.BggResponse;

namespace Main.Data;

public class BoardGameInfoService
{
    public bool KnowsGames { get => File.Exists(_outputPath); }

    private static readonly Regex _exTrash = new(@"[\-':()!?+/\\]", RegexOptions.Compiled);
    private static readonly Regex _exNormalLetters = new(@"[a-zA-Z]", RegexOptions.Compiled);

    private Lazy<BoardGameDump[]> _boardGameInfo = new();

    private readonly string _outputPath;

    private readonly object _locker = new object();

    public BoardGameInfoService(IWebHostEnvironment hostEnvironment)
    {
        _outputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames.json");
        ResetBoardGameInfo();
    }

    public async Task<List<FoundItem>> SolvePuzzleAsync(char[][] letterArray)
    {
        int height = letterArray.Length;
        if (!letterArray.All(row => row.Length == height))
            throw new ArgumentException("Given array isn't a square");

        letterArray = letterArray.Select(row => row.Select(c => char.ToUpper(c)).ToArray()).ToArray();
        (string, int)[] wordsToGame = _boardGameInfo.Value.SelectMany(game => game.Names.Select(name => (name, game.Id))).DistinctBy(g => g.name).ToArray();
        List<FoundItem> result = new();
        await Task.Run(() =>
        {
            Parallel.ForEach(letterArray, (row, _, i) =>
            {
                Parallel.ForEach(letterArray, async (item, _, j) =>
                {
                    FoundItem[] answers = await GetAnswersAsync(letterArray, i, j);
                    if ((answers ?? Array.Empty<FoundItem>()).Length > 0)
                    {
                        lock (_locker)
                            result.AddRange(answers!);
                    }
                });
            });
        });

        return result;
    }

    private Task<FoundItem[]> GetAnswersAsync(char[][] letterArray, long i, long j)
    {

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
        ResetBoardGameInfo();
    }

    private static BoardGameDump? CreateDump(Boardgame boardGame)
    {
        if (boardGame == null)
            return null;

        BoardGameDump dump = new(int.Parse(boardGame.Objectid), boardGame.Name.Single(n => n.Primary == "true").Text);

        List<string> names = boardGame?.Name?.Select(n => _exTrash.Replace(n.Text, string.Empty).RemoveDiacritics()).Distinct().ToList() ?? new();
        string[] partialNames = names.SelectMany(n => n.Split(' ').Take(2).Where(w => w.Length > 3)).ToArray();
        names.AddRange(partialNames);

        dump.Names = names.Where(n => _exNormalLetters.IsMatch(n)).Select(n => n.Replace(" ", string.Empty).ToUpper()).Distinct().ToList();
        return dump;
    }

    private void ResetBoardGameInfo()
    {
        _boardGameInfo = new Lazy<BoardGameDump[]>(() => JsonConvert.DeserializeObject<BoardGameDump[]>(File.ReadAllText(_outputPath))
            ?? Array.Empty<BoardGameDump>());
    }
}
