using System.Timers;
using Timer = System.Timers.Timer;

namespace Main.Data;

public class BoardGameInfoSubscriber : IHostedService
{
    private static bool _subscribed;
    private readonly BoardGameInfoService _bgiService;
    private Timer _timer = new();
    private readonly ElapsedEventHandler _timerCallback;

    public BoardGameInfoSubscriber(BoardGameInfoService bgiService)
    {
        _bgiService = bgiService;
        _timerCallback = (s, e) => Task.Run(() => _bgiService.FetchDataAsync());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new(10_000) { AutoReset = true, Enabled = true };
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
