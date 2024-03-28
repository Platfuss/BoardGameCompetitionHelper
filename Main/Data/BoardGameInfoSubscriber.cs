using System.Timers;
using Timer = System.Timers.Timer;

namespace Main.Data;

public class BoardGameInfoSubscriber : IHostedService
{
    private static readonly double _twoWeeks = 14 * 24 * 60 * 60 * 1_000;
    private static bool _subscribed;
    private Timer _timer = new();
    private readonly ElapsedEventHandler _timerCallback;

    public BoardGameInfoSubscriber(BoardGameInfoService bgiService, IWebHostEnvironment hostEnvironment)
    {
        File.AppendAllText(Path.Combine(hostEnvironment!.WebRootPath, "Log_test.txt"), $"{DateTime.Now:s} ===> Application started\n");
        //if (!bgiService.KnowsGames)
        Task.Run(() => bgiService.FetchDataAsync());
        _timerCallback = (s, e) => Task.Run(() => bgiService.FetchDataAsync());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new(_twoWeeks) { AutoReset = true, Enabled = true };
        if (!_subscribed)
        {
            _timer.Elapsed += _timerCallback;
            _subscribed = true;
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Elapsed -= _timerCallback;
        return Task.CompletedTask;
    }
}
