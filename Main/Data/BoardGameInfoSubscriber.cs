using System.Timers;
using Timer = System.Timers.Timer;

namespace Main.Data;

public class BoardGameInfoSubscriber : IHostedService
{
    private static bool _subscribed;
    private Timer _timer = new();
    private readonly ElapsedEventHandler _timerCallback;

    public BoardGameInfoSubscriber(BoardGameInfoService bgiService)
    {
#if !DEBUG
        if (!bgiService.KnowsGames)
            Task.Run(() => bgiService.FetchDataAsync());
#endif
        _timerCallback = (s, e) => Task.Run(() => bgiService.FetchDataAsync());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new(7 * 24 * 60 * 60 * 1_000) { AutoReset = true, Enabled = true };
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
