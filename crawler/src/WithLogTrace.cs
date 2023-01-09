namespace tbm.Crawler
{
    public abstract class WithLogTrace
    {
        private IConfigurationSection? _configLogTrace;
        private readonly Timer _timerLogTrace = new() {Enabled = true};

        protected abstract void LogTrace();

        protected void InitLogTrace(IConfigurationSection config)
        {
            _configLogTrace = config.GetSection("LogTrace");
            _timerLogTrace.Interval = _configLogTrace.GetValue("LogIntervalMs", 1000);
            _timerLogTrace.Elapsed += (_, _) => LogTrace();
        }

        protected bool ShouldLogTrace()
        {
            if (_configLogTrace == null || !_configLogTrace.GetValue("Enabled", false)) return false;
            _timerLogTrace.Interval = _configLogTrace.GetValue("LogIntervalMs", 1000);
            return true;
        }
    }
}
