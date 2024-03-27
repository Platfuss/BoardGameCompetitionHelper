using Main.Data.DTO;
using Main.Data.Helper;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static Main.Data.DTO.BggResponse;

namespace Main.Data;

public class BoardGameInfoService
{
    public bool KnowsGames => File.Exists(_outputPath);

    private static readonly Regex _exTrash = new(@"[\-':()!?+/\\]|\(.+?\)", RegexOptions.Compiled);
    private static readonly Regex _exNormalLetters = new(@"[a-zA-Z]", RegexOptions.Compiled);

    private Lazy<BoardGameDump[]> _boardGameInfo = new();

    private readonly string _outputPath, _temporaryOutputPath, _logPath;

    private readonly object _locker = new();

    public BoardGameInfoService(IWebHostEnvironment hostEnvironment)
    {
        _outputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames.json");
        _temporaryOutputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames_tmp.json");
        _logPath = Path.Combine(hostEnvironment!.WebRootPath, "Logs.json");
        ResetBoardGameInfo();
    }

    public async Task<List<FoundItem>> SolvePuzzleAsync(char[][] letterArray)
    {
        int height = letterArray.Length;
        if (!letterArray.All(row => row.Length == height))
            throw new ArgumentException("Given array isn't a square");

        letterArray = letterArray.Select(row => row.Select(c => char.ToUpper(c)).ToArray()).ToArray();
        (string, int)[] wordsToGameId = _boardGameInfo.Value.SelectMany(game => game.Names.Select(name => (name, game.Id))).DistinctBy(g => g.name).ToArray();
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

        return result.OrderBy(r => r.BggId).DistinctBy(r => r.Name).ToList();
    }

    private static List<FoundItem> GetAnswers((string, int)[] wordsToGameId, char[][] letterArray, int i, int j)
    {
        List<FoundItem> answers = new();
        foreach (Directions direction in Enum.GetValues(typeof(Directions)))
        {
            foreach (string word in DirectionsChecker.GetWords(direction, letterArray, i, j))
            {
                (string, int) existingWord = wordsToGameId.FirstOrDefault(wtg => wtg.Item1 == word);
                if (existingWord != default)
                {
                    FoundItem foundItem = new() { Name = existingWord.Item1, BggId = existingWord.Item2, Direction = direction, StartingPosition = (i, j) };
                    answers.Add(foundItem);
                }
            }
        }

        return answers;
    }

    internal async Task FetchDataAsync()
    {
        HttpClient client = new() { BaseAddress = new Uri("https://boardgamegeek.com/xmlapi/boardgame/") };
        int step = 300;
        int index = GetStartingIndex();

        bool readAllGames = false;
        do
        {
            List<BoardGameDump> dumps = new();
            for (int i = 0; i < 10; i++)
            {
                HttpResponseMessage response = await client.GetAsync(string.Join(',', Enumerable.Range(index, step)));
                index += step;

                Boardgames bggResponse = XmlReader<Boardgames>.Deserialize(response.Content.ReadAsStringAsync().Result);
                dumps.AddRange(bggResponse.Boardgame?.Select(b => CreateDump(b)).Where(d => d != null).ToArray()!);

                await Console.Out.WriteLineAsync($"{DateTime.Now:s} ===> {index - 1}");

                if (bggResponse?.Boardgame?.Count < 2)
                {
                    readAllGames = true;
                    AppendGameInfo(dumps, index, finished: true);
                    break;
                }
            }

            if (!readAllGames)
            {
                AppendGameInfo(dumps, index, finished: false);
                Thread.Sleep(5 * 60 * 1_000);
            }

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
    }

    private static BoardGameDump? CreateDump(Boardgame boardGame)
    {
        if (boardGame == null || boardGame.Objectid == null || boardGame.Name.Count == 0)
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
