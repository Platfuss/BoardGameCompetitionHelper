
namespace Main.Data;

public class WakeUpCaller : BackgroundService
{
    private PeriodicTimer? _timer;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new(TimeSpan.FromMinutes(15));
        HttpClient client = new();

        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            _ = await client.GetAsync("https://misz-masz.netlify.app/ping");
        }
    }
}
