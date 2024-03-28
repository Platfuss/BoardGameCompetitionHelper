using Main.Data.DTO;
using Main.Data.Helper;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using static Main.Data.DTO.BggResponse;

namespace Main.Data;

public class BoardGameInfoService
{
    public bool KnowsGames => File.Exists(_outputPath);

    private static readonly Regex _exTrash = new(@"\(.+?\)|[\-':()!?+/\\.]", RegexOptions.Compiled);
    private static readonly Regex _exNormalLetters = new(@"[a-zA-Z]", RegexOptions.Compiled);

    private Lazy<BoardGameDump[]> _boardGameInfo = new();

    private readonly string _outputPath, _temporaryOutputPath, _logPath, _testPath;

    private readonly object _locker = new();

    public BoardGameInfoService(IWebHostEnvironment hostEnvironment)
    {
        _outputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames.json");
        _temporaryOutputPath = Path.Combine(hostEnvironment!.WebRootPath, "KnownBoardGames_tmp.json");
        _logPath = Path.Combine(hostEnvironment!.WebRootPath, "Logs.json");

        _testPath = Path.Combine(hostEnvironment!.WebRootPath, "Log_test.txt");

        ResetBoardGameInfo();
    }

    public async Task<List<FoundItem>> SolvePuzzleAsync(char[][] letterArray)
    {
        int height = letterArray.Length;
        if (!letterArray.All(row => row.Length == height))
            throw new ArgumentException("Given array isn't a square");

        letterArray = letterArray.Select(row => row.Select(c => char.ToUpper(c)).ToArray()).ToArray();
        (string, int)[] wordsToGameId = _boardGameInfo.Value.SelectMany(game => game.Names.Where(n => n.Length <= height).Select(name => (name, game.Id))).DistinctBy(g => g.name).ToArray();
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
        int step = 200;
        int index = GetStartingIndex();

        bool readAllGames = false;
        File.AppendAllText(_testPath, $"{DateTime.Now:s} ===> Start fetching\n");
        do
        {
            List<BoardGameDump> dumps = new();
            for (int i = 0; i < 10; i++)
            {
                File.AppendAllText(_testPath, "93 ");
                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(string.Join(',', Enumerable.Range(index, step)));
                }
                catch
                {
                    File.AppendAllText(_testPath, "\nException Thrown\n");
                    i--;
                    continue;
                }

                File.AppendAllText(_testPath, "95 ");
                index += step;

                Boardgames bggResponse = XmlReader<Boardgames>.Deserialize(response.Content.ReadAsStringAsync().Result);
                File.AppendAllText(_testPath, "99 ");
                dumps.AddRange(bggResponse.Boardgame?.Select(b => CreateDump(b)).Where(d => d != null).ToArray()!);

                await Console.Out.WriteLineAsync($"{i + 1:D2}: {DateTime.Now:s} ===> {index - 1}");
                await File.AppendAllTextAsync(_testPath, $"\n{DateTime.Now:s} ===> {i + 1:D2}: {index - 1}\n");

                if (bggResponse?.Boardgame?.Count < 2)
                {
                    readAllGames = true;
                    File.AppendAllText(_testPath, "108 ");
                    AppendGameInfo(dumps, index, finished: true);
                    await File.AppendAllTextAsync(_testPath, $"{DateTime.Now:s} ===> Read all games\n");
                    break;
                }
                File.AppendAllText(_testPath, "113 ");
                //Thread.Sleep(20 * 1_000);
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
        File.AppendAllText(_testPath, "139 ");
        var previousDumps = File.Exists(_temporaryOutputPath)
            ? JsonConvert.DeserializeObject<List<BoardGameDump>>(File.ReadAllText(_temporaryOutputPath))
            : new();

        previousDumps!.AddRange(dumps);

        File.AppendAllText(_testPath, "146 ");
        File.WriteAllText(_temporaryOutputPath, JsonConvert.SerializeObject(previousDumps));
        if (finished)
        {
            if (File.Exists(_outputPath))
                File.Delete(_outputPath);
            File.Move(_temporaryOutputPath, _outputPath);
        }

        File.AppendAllText(_testPath, "155 ");
        Log log = new() { IsFinished = finished, LastUpdate = DateTime.Now, LastIndex = currentIndex };
        File.WriteAllText(_logPath, JsonConvert.SerializeObject(log));

        Console.WriteLine("159");
        Console.WriteLine($"\tAppended {dumps.Count} games. Current Index: {currentIndex}");
        File.AppendAllText(_testPath, $"\t{DateTime.Now:s} ===> Appended {dumps.Count} games. Current Index: {currentIndex}\n");
    }

    private BoardGameDump? CreateDump(Boardgame boardGame)
    {
        File.AppendAllText(_testPath, "166 ");
        if (boardGame == null || boardGame.Objectid == null || boardGame.Name.Count == 0)
            return null;

        File.AppendAllText(_testPath, "170 ");
        BoardGameDump dump = new(int.Parse(boardGame.Objectid), boardGame.Name.Single(n => n.Primary == "true").Text);

        File.AppendAllText(_testPath, "173 ");
        List<string> names = boardGame.Name.Select(n => _exTrash.Replace(n.Text, string.Empty).RemoveDiacritics()).Distinct().ToList();
        File.AppendAllText(_testPath, "175 ");
        string[] partialNames = names.SelectMany(n => n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Where(w => w.Length > 3)).ToArray();
        names.AddRange(partialNames);

        File.AppendAllText(_testPath, "179 ");
        dump.Names = names.Where(n => _exNormalLetters.IsMatch(n)).Select(n => n.Replace(" ", string.Empty).ToUpper()).Distinct().ToList();
        return dump;
    }

    private void ResetBoardGameInfo()
    {
        _boardGameInfo = new Lazy<BoardGameDump[]>(() => JsonConvert.DeserializeObject<BoardGameDump[]>(File.ReadAllText(_outputPath))
            ?? Array.Empty<BoardGameDump>());
    }
}
