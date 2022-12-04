namespace tbm.Crawler.Tieba
{
    public class ClientRequesterTcs : WithLogTrace
    {
        private readonly ILogger<ClientRequesterTcs> _logger;
        private readonly IConfigurationSection _config;
        private readonly ConcurrentQueue<TaskCompletionSource> _queue = new();
        private readonly Timer _timer = new() {Enabled = true};
        private double _maxRps;
        private readonly Stopwatch _stopWatch = new();
        private int _requestCounter;

        private int QueueLength => _queue.Count;
        private float AverageRps => _requestCounter / (float)_stopWatch.Elapsed.TotalSeconds;
        private double MaxRps
        {
            get => _maxRps;
            set
            {
                _maxRps = value;
                if ((uint)_timer.Interval != (uint)(1000 / value))
                { // only update interval with a truncated integer to prevent frequently change it
                  // which will cause the increment of real rps can't keep up with _maxRps with long queue length
                    _timer.Interval = 1000 / value;
                }
                _ = Interlocked.Increment(ref _requestCounter);
            }
        }

        public ClientRequesterTcs(ILogger<ClientRequesterTcs> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config.GetSection("ClientRequesterTcs");
            InitLogTrace(_config);
            MaxRps = _config.GetValue("InitialRps", 15);
            _stopWatch.Start();

            _timer.Elapsed += (_, _) =>
            {
                if (_queue.TryDequeue(out var tcs)) tcs.SetResult();
            };
        }

        protected override void LogTrace()
        {
            if (!ShouldLogTrace()) return;
            _logger.LogTrace("TCS: queueLen={} maxLimitRps={:F2} avgRps={:F2} elapsed={:F1}s",
                QueueLength, MaxRps, AverageRps, _stopWatch.Elapsed.TotalSeconds);
            if (_config.GetValue("LogTrace:ResetAfterLog", false)) ResetAverageRps();
        }

        public void Increase() => MaxRps = Math.Min(
            _config.GetValue("LimitRps:1", 1000),
            MaxRps + _config.GetValue("DeltaRps:0", 0.01));

        public void Decrease() => MaxRps = Math.Max(
            _config.GetValue("LimitRps:0", 1),
            MaxRps - _config.GetValue("DeltaRps:1", 0.5));

        public void Wait()
        { // https://devblogs.microsoft.com/premier-developer/the-danger-of-taskcompletionsourcet-class/
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(tcs);
            tcs.Task.Wait();
        }

        private void ResetAverageRps()
        {
            _ = Interlocked.Exchange(ref _requestCounter, 0);
            _stopWatch.Restart();
        }
    }
}
