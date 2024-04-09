using HtmlAgilityPack;
using Main.Data.DTO;
using Main.Data.Helper;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using System.Text.RegularExpressions;
using static Main.Data.DTO.BggResponse;

namespace Main.Data;

public class BoardGameInfoService
{
    public bool KnowsGames => File.Exists(_outputPath);

    private static readonly Regex _exTrash = new(@"\(.+?\)|[^a-zA-Z0-9\sĄąĆćĘęŁłŃńÓóŚśŻżŹź]", RegexOptions.Compiled);

    private Lazy<BoardGameDump[]> _boardGameInfo = new();

    private readonly string _outputPath, _temporaryOutputPath, _logPath;

    private readonly object _locker = new();

    private readonly RetryPolicy _retryPolicy = Policy.Handle<Exception>().WaitAndRetry(retryCount: 16, i => TimeSpan.FromSeconds(i * 60));

    public BoardGameInfoService(IWebHostEnvironment hostEnvironment)
    {
        _outputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames.json");
        _temporaryOutputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames_tmp.json");
        _logPath = Path.Combine(hostEnvironment!.WebRootPath, "Logs.json");

        ResetBoardGameInfo();
    }

    public async Task<List<FoundItem>> SolvePuzzleAsync(List<char[]> letterArray)
    {
        int height = letterArray.Count;
        if (!letterArray.All(row => row.Length == height))
            throw new ArgumentException("Given array isn't a square");

        letterArray = letterArray.Select(row => row.Select(c => char.ToUpper(c)).ToArray()).ToList();
        (string, int, (int?, string?))[] wordsToGameId = _boardGameInfo.Value.SelectMany(game => game.Names.Where(n => n.Length <= height).Select(name => (name, game.Id, (game.LanguageId, game.SecondayFullName)))).DistinctBy(g => g.name).ToArray();
        List<FoundItem> result = new();
        await Task.Run(() =>
        {
            Parallel.ForEach(letterArray, (row, _, i) =>
            {
                Parallel.ForEach(letterArray, (item, _, j) =>
                {
                    List<FoundItem> answers = GetAnswers(wordsToGameId, letterArray, (int)i, (int)j);
                    if ((answers ?? new()).Count > 0)
                    {
                        lock (_locker)
                            result.AddRange(answers!);
                    }
                });
            });
        });

        return result.OrderByDescending(r => r.Length).ThenBy(r => r.BggId).DistinctBy(r => r.Name).Aggregate(new List<FoundItem>(), (sum, item) =>
        {
            if (sum.Any(s => s.StartingPosition == item.StartingPosition && s.Direction == item.Direction))
                return sum;
            else
                return [.. sum, item];
        });
    }

    private static List<FoundItem> GetAnswers((string, int, (int?, string?))[] wordsToGameId, List<char[]> letterArray, int i, int j)
    {
        List<FoundItem> answers = [];
        foreach (Directions direction in Enum.GetValues(typeof(Directions)))
        {
            foreach (string word in DirectionsChecker.GetWords(direction, letterArray, i, j))
            {
                (string, int, (int? LanguageId, string? SecondaryName)) existingWord = wordsToGameId.FirstOrDefault(wtg => wtg.Item1 == word);
                if (existingWord != default)
                {
                    FoundItem foundItem = new() { Name = existingWord.Item1, BggId = existingWord.Item2, Direction = direction, StartingPosition = (i, j), LanguageInfo = (existingWord.Item3.LanguageId, existingWord.Item3.SecondaryName) };
                    answers.Add(foundItem);
                }
            }
        }

        return answers;
    }

    internal void FetchData()
    {
        HttpClient client = new() { BaseAddress = new Uri("https://boardgamegeek.com/xmlapi/boardgame/") };
        int step = 200;
        int index = GetStartingIndex();

        bool readAllGames = false;
        do
        {
            List<BoardGameDump> dumps = new();
            for (int i = 0; i < 10; i++)
            {
                HttpResponseMessage response = new();
                _retryPolicy.Execute(() => response = client.GetAsync(string.Join(',', Enumerable.Range(index, step))).Result);

                Boardgames bggResponse = XmlReader<Boardgames>.Deserialize(response.Content.ReadAsStringAsync().Result);

                index += step;
                dumps.AddRange(bggResponse.Boardgame?.Select(b => CreateDump(b)).Where(d => d != null).ToArray()!);

                Console.Out.WriteLine($"{i + 1:D2}: {DateTime.Now:s} ===> {index - 1}");

                if (bggResponse?.Boardgame?.Count < 2)
                {
                    readAllGames = true;
                    AppendGameInfo(dumps, index, finished: true);
                    break;
                }
            }

            if (!readAllGames)
                AppendGameInfo(dumps, index, finished: false);

        } while (!readAllGames);

        ResetBoardGameInfo();
    }

    private int GetStartingIndex()
    {
        if (!File.Exists(_logPath))
            return 1;

        var log = JsonConvert.DeserializeObject<Log>(File.ReadAllText(_logPath));
        if (log == null || log.IsFinished)
            return 1;
        else
            return log.LastIndex;
    }

    private void AppendGameInfo(List<BoardGameDump> dumps, int currentIndex, bool finished)
    {
        var previousDumps = File.Exists(_temporaryOutputPath)
            ? JsonConvert.DeserializeObject<List<BoardGameDump>>(File.ReadAllText(_temporaryOutputPath))
            : new();

        previousDumps!.AddRange(dumps);

        File.WriteAllText(_temporaryOutputPath, JsonConvert.SerializeObject(previousDumps));
        if (finished)
        {
            if (File.Exists(_outputPath))
                File.Delete(_outputPath);
            File.Move(_temporaryOutputPath, _outputPath);
        }

        Log log = new() { IsFinished = finished, LastUpdate = DateTime.Now, LastIndex = currentIndex };
        File.WriteAllText(_logPath, JsonConvert.SerializeObject(log));

        Console.WriteLine($"\tAppended {dumps.Count} games. Current Index: {currentIndex}");
    }

    private BoardGameDump? CreateDump(Boardgame boardGame)
    {
        if (boardGame == null || boardGame.Objectid == null || boardGame.Name.Count == 0 || IsVideoGame(boardGame))
            return null;

        Boardgameversion? polishVersion = boardGame.Boardgameversion?.Where(v => v.Text.Contains("polish", StringComparison.InvariantCultureIgnoreCase)).OrderByDescending(v => v.Objectid).FirstOrDefault();
        if (polishVersion == null)
            return null;

        HttpClient client = new() { BaseAddress = new Uri("https://boardgamegeek.com/boardgameversion/") };
        HttpResponseMessage response = new();
        _retryPolicy.Execute(() => response = client.GetAsync(polishVersion.Objectid).Result);

        HtmlDocument document = new();
        document.LoadHtml(response.Content.ReadAsStringAsync().Result);

        string polishName = document.DocumentNode.SelectSingleNode("//div[@id='edit_linkednameid']").InnerText.Trim();
        string originalName = boardGame.Name.Single(n => n.Primary == "true").Text;

        BoardGameDump dump = new(int.Parse(boardGame.Objectid), originalName)
        {
            LanguageId = int.Parse(polishVersion.Objectid ?? "-1"),
            SecondayFullName = polishName,
        };

        List<string> names = new string[] { originalName, polishName }.Select(n => _exTrash.Replace(n, string.Empty).RemoveDiacritics()).ToList();
        string[] partialNames = names.SelectMany(n => n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3)).ToArray();
        names.AddRange(partialNames);

        dump.Names = names.Select(n => n.ToUpper()).Distinct().ToList();
        return dump;
    }

    private void ResetBoardGameInfo()
    {
        _boardGameInfo = new Lazy<BoardGameDump[]>(() => JsonConvert.DeserializeObject<BoardGameDump[]>(File.ReadAllText(_outputPath))
            ?? []);
    }

    private static bool IsVideoGame(Boardgame boardGame) => boardGame.Videogamedeveloper != null || (boardGame.Videogamepublisher ?? new()).Count > 0 || (boardGame.Videogamecompilation ?? new()).Count > 0 || (boardGame.Videogameplatform ?? new()).Count > 0 || (boardGame.Videogameversion ?? new()).Count > 0 || boardGame.Videogamegenre != null || boardGame.Videogamemode != null || boardGame.Videogametheme != null;
}