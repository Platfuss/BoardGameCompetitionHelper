namespace Main.Data;

public class BoardGameInfoSubscriber : BackgroundService
{
    private PeriodicTimer? _timer;
    private readonly BoardGameInfoService _bgiService;

    public BoardGameInfoSubscriber(BoardGameInfoService bgiService, IWebHostEnvironment hostEnvironment)
    {
        File.AppendAllText(Path.Combine(hostEnvironment!.WebRootPath, "Log_test.txt"), $"{DateTime.Now:s} ===> Application started\n");
        _bgiService = bgiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new(TimeSpan.FromDays(14));

        if (!_bgiService.KnowsGames)
            await _bgiService.FetchDataAsync();

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await _bgiService.FetchDataAsync();
        }
    }
}
