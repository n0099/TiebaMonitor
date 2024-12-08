namespace tbm.Crawler.Tieba;

public sealed class ClientRequesterTcs : WithLogTrace
{
    private readonly ILogger<ClientRequesterTcs> _logger;
    private readonly IConfigurationSection _config;
    private readonly ConcurrentQueue<TaskCompletionSource> _queue = new();
    private readonly Timer _timer = new() {Enabled = true};
    private readonly Stopwatch _stopwatch = new();
    private uint _requestCounter;

    public ClientRequesterTcs(ILogger<ClientRequesterTcs> logger, IConfiguration config)
        : base(config, "ClientRequesterTcs")
    {
        _logger = logger;
        _config = config.GetSection("ClientRequesterTcs");
        MaxRps = _config.GetValue("InitialRps", 15);
        _stopwatch.Start();

        _timer.Elapsed += (_, _) =>
        {
            if (_queue.TryDequeue(out var tcs)) tcs.SetResult();
        };
    }

    private uint QueueLength => (uint)_queue.Count;
    private float AverageRps => _requestCounter / (float)_stopwatch.Elapsed.TotalSeconds;
    private double MaxRps
    {
        get;
        set
        {
            field = value;
            if ((uint)_timer.Interval != (uint)(1000 / value))
            { // only update interval with a truncated integer to prevent frequently change it
                // which will cause the increment of real rps can't keep up with _maxRps with long queue length
                _timer.Interval = 1000 / value;
            }
            _ = Interlocked.Increment(ref _requestCounter);
        }
    }

    public override void Dispose()
    {
        _timer.Dispose();
        base.Dispose();
    }

    public void Increase() => MaxRps = Math.Min(
        _config.GetValue("LimitRps:1", 1000),
        MaxRps + _config.GetValue("DeltaRps:0", 0.01));

    public void Decrease() => MaxRps = Math.Max(
        _config.GetValue("LimitRps:0", 1),
        MaxRps - _config.GetValue("DeltaRps:1", 0.5));

    public async Task Wait(CancellationToken stoppingToken = default)
    { // https://devblogs.microsoft.com/premier-developer/the-danger-of-taskcompletionsourcet-class/
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(tcs);
        await tcs.Task.WaitAsync(stoppingToken);
    }

    protected override void LogTrace()
    {
        if (!ShouldLogTrace()) return;
        _logger.LogTrace("TCS: queueLen={} maxLimitRps={:F2} avgRps={:F2} elapsed={:F1}s",
            QueueLength, MaxRps, AverageRps, _stopwatch.Elapsed.TotalSeconds);
        if (_config.GetValue("LogTrace:ResetAfterLog", false)) ResetAverageRps();
    }

    private void ResetAverageRps()
    {
        _ = Interlocked.Exchange(ref _requestCounter, 0);
        _stopwatch.Restart();
    }
}
