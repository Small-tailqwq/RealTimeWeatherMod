namespace UnityExplorerMcp.Core
{
    internal class ConsoleCaptureService
    {
        public const int MaxLogs = 500;

        private readonly BepInEx.Logging.ManualLogSource _logger;
        private readonly System.Collections.Generic.List<LogEntry> _logs =
            new System.Collections.Generic.List<LogEntry>();
        private readonly object _lock = new object();

        public ConsoleCaptureService(BepInEx.Logging.ManualLogSource logger)
        {
            _logger = logger;
        }

        public void Start()
        {
            UnityEngine.Application.logMessageReceivedThreaded += OnLogMessage;
        }

        public void Stop()
        {
            UnityEngine.Application.logMessageReceivedThreaded -= OnLogMessage;
        }

        private void OnLogMessage(string condition, string stackTrace, UnityEngine.LogType type)
        {
            lock (_lock)
            {
                _logs.Add(new LogEntry
                {
                    time = System.DateTime.Now.ToString("HH:mm:ss.fff"),
                    level = MapLogType(type),
                    message = condition,
                    stackTrace = stackTrace
                });

                while (_logs.Count > MaxLogs)
                    _logs.RemoveAt(0);
            }
        }

        public System.Collections.Generic.List<LogEntry> GetRecent(int count = 50)
        {
            lock (_lock)
            {
                if (count <= 0 || count > _logs.Count)
                    count = _logs.Count;
                return _logs.GetRange(_logs.Count - count, count);
            }
        }

        private static string MapLogType(UnityEngine.LogType type)
        {
            switch (type)
            {
                case UnityEngine.LogType.Error: return "error";
                case UnityEngine.LogType.Assert: return "assert";
                case UnityEngine.LogType.Warning: return "warning";
                case UnityEngine.LogType.Log: return "log";
                case UnityEngine.LogType.Exception: return "exception";
                default: return "log";
            }
        }
    }

    internal class LogEntry
    {
        public string time { get; set; }
        public string level { get; set; }
        public string message { get; set; }
        public string stackTrace { get; set; }
    }
}
