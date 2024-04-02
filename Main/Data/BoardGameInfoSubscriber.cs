namespace Main.Data;

public class BoardGameInfoSubscriber : BackgroundService
{
    private PeriodicTimer? _timer;
    private readonly BoardGameInfoService _bgiService;

    // TODO self calling aps
    public BoardGameInfoSubscriber(BoardGameInfoService bgiService, IWebHostEnvironment hostEnvironment)
    {
        _bgiService = bgiService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new(TimeSpan.FromDays(14));

        //if (!_bgiService.KnowsGames)
        await Task.Run(() => _bgiService.FetchData(), stoppingToken);

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            await Task.Run(() => _bgiService.FetchData(), stoppingToken);
        }
    }
}
