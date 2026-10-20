using System;
using System.Collections.Generic;
using Bulbul;
using UnityEngine;

namespace ChillWithYou.EnvSync.Core
{
    /// <summary>
    /// Developer-facing seam for deterministic environment scenarios. It owns
    /// validation, immediate application and runtime verification. Playback sessions
    /// override inputs in memory and leave the user's saved configuration intact.
    /// </summary>
    public static class WeatherDebugControl
    {
        private static readonly EnvironmentType[] BaseEnvironments =
            ModEnvironmentMap.BaseEnvironments;

        private static readonly EnvironmentType[] SceneryWeathers =
            ModEnvironmentMap.SceneryEnvironments;

        internal static DebugPlayback Playback { get; private set; }
        internal static bool IsDebugActive => Playback != null || ChillEnvPlugin.Cfg_DebugMode.Value;
        internal static bool SmoothEnabled => Playback?.Smooth ?? ChillEnvPlugin.Cfg_EnableSmoothTransitions.Value;
        internal static SyncPolicySnapshot BuildPolicy() => SyncPolicy.Build(
            Playback != null || ChillEnvPlugin.Cfg_EnableTimeSync.Value,
            Playback != null || ChillEnvPlugin.Cfg_EnableWeatherSync.Value,
            ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value);

        private static TimeTransitionWindow _testWindow;
        private static bool _testCycle;
        private static readonly int[] _frameHistogram = new int[1001];
        private static int _frames, _over50ms, _gcStart;
        private static double _frameTotal, _frameMax, _measureAfter;

        private static DebugPlayback _manualPlayback;
        private static EnvironmentType? _previewBase;
        private static readonly Dictionary<EnvironmentType, bool> _previewScenery = new Dictionary<EnvironmentType, bool>();
        internal static bool ManualPreviewActive => Playback != null && Playback == _manualPlayback;

        internal static void StartManualPreview()
        {
            if (!IsConfigurationReady() || !ChillEnvPlugin.Cfg_DebugMode.Value || ManualPreviewActive) return;
            var runtime = GameEnvironmentRuntime.Shared;
            _previewBase = null;
            foreach (var env in BaseEnvironments)
                if (runtime.TryIsWindowActive(env, out bool active) && active) { _previewBase = env; break; }
            _previewScenery.Clear();
            foreach (var env in SceneryWeathers)
                if (runtime.TryIsWindowActive(env, out bool active)) _previewScenery[env] = active;
            var input = ModEnvironmentContext.Capture();
            var now = input.Now;
            input = input.WithWeather(PreviewWeather(false, input.Weather?.Temperature ?? 20));
            input.IsDebug = true;
            input.ConfigurationValid = true;
            input.ConfigurationError = null;
            input.Now = now.Date;
            input.Sunrise = TimeSpan.FromHours(6.5);
            input.Sunset = TimeSpan.FromHours(18.5);
            Playback = _manualPlayback = new DebugPlayback(input, now.Date.AddDays(1), 0, true,
                Time.realtimeSinceStartupAsDouble);
            Playback.Seek(now, Time.realtimeSinceStartupAsDouble);
            _testCycle = true;
            GameEnvironmentRuntime.Shared.Transitions.Dispose();
            TriggerImmediateApplication();
        }

        internal static void SetManualPreview(int minute, bool cloudy)
            => SetManualWeatherPreview(minute, cloudy ? 9 : 1);

        internal static void SetManualWeatherPreview(int minute, int weatherCode)
        {
            if (!ManualPreviewActive) return;
            minute = Math.Max(0, Math.Min(1439, minute));
            var time = Playback.Inputs.Now.Date.AddMinutes(minute);
            if (Playback.Now(Time.realtimeSinceStartupAsDouble) != time)
                Playback.Seek(time, Time.realtimeSinceStartupAsDouble);
            Playback.Inputs.Weather = new Models.WeatherInfo { Code = weatherCode,
                Temperature = Playback.Inputs.Weather.Temperature, Text = "Weather preview",
                Condition = Models.WeatherConditionMapper.FromSeniverseCode(weatherCode) };
            // GUI input arrives after Update; present this seek without waiting for the next tick.
            GameEnvironmentRuntime.Shared.Transitions.Tick(BuildPolicy(), ModEnvironmentContext.Capture());
            TriggerImmediateApplication();
        }

        private static Models.WeatherInfo PreviewWeather(bool cloudy, int temperature) =>
            new Models.WeatherInfo { Code = cloudy ? 9 : 1, Temperature = temperature,
                Text = cloudy ? "Cloudy preview" : "Clear preview",
                Condition = Models.WeatherConditionMapper.FromSeniverseCode(cloudy ? 9 : 1) };

        internal static void StopManualPreview()
        {
            if (!ManualPreviewActive) return;
            StopPlayback();
            _manualPlayback = null;
            var runtime = GameEnvironmentRuntime.Shared;
            runtime.Precipitation.Release();
            // Disabled sync channels cannot restore the scene through the normal sync request.
            if (!ChillEnvPlugin.IsInCutscene() && !SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                if (!ChillEnvPlugin.Cfg_EnableTimeSync.Value && _previewBase.HasValue)
                    runtime.TryApplyBaseImmediately(_previewBase.Value, EnvironmentApplyMode.Interactive);
                if (!ChillEnvPlugin.Cfg_EnableWeatherSync.Value)
                    foreach (var pair in _previewScenery)
                        runtime.TryApplySceneryState(pair.Key, pair.Value, EnvironmentApplyMode.Interactive,
                            applyPrecipitationSoundSetting: false);
            }
            _previewBase = null;
            _previewScenery.Clear();
            TriggerImmediateApplication();
        }

        public static string ControlPlayback(string json)
        {
            if (!IsConfigurationReady()) return SerializeError("Mod 配置尚未初始化");
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return SerializeError("测试命令不能为空");
                var command = new PlaybackCommand();
                JsonUtility.FromJsonOverwrite(json, command);
                double realtime = Time.realtimeSinceStartupAsDouble;
                if (command.action == "test_start" || command.action == "test_cycle")
                {
                    bool cycle = command.action == "test_cycle";
                    if (cycle && (command.days < 1 || command.days > 7))
                        return SerializeError("days 必须介于 1 和 7");
                    var rise = TimeSpan.FromHours(6.5);
                    var set = TimeSpan.FromHours(18.5);
                    int windowIndex;
                    switch (command.boundary)
                    {
                        case "dawn": windowIndex = 0; break;
                        case "sunset": windowIndex = 1; break;
                        case "night": windowIndex = 2; break;
                        default: return SerializeError("boundary 必须是 dawn、sunset 或 night");
                    }
                    var window = TimeTransitionSchedule.Windows(DateTime.Today, rise, set)[windowIndex];
                    var start = cycle ? DateTime.Today : window.Start.AddSeconds(-15);
                    var end = cycle ? start.AddDays(command.days) : window.End.AddSeconds(15);
                    var input = new DebugScenarioInput { WeatherCode = command.weatherCode,
                        Temperature = command.temperature, WeatherText = "TransitionTest",
                        CurrentDateTime = start.ToString("yyyy-MM-dd HH:mm:ss"),
                        Sunrise = "06:30", Sunset = "18:30" };
                    if (!DebugScenario.TryCreate(input, DateTime.Now, out var scenario, out var error))
                        return SerializeError(error);
                    var snapshot = new EnvironmentInputSnapshot { IsDebug = true, ConfigurationValid = true,
                        Now = scenario.Now, Sunrise = rise, Sunset = set, Weather = scenario.Weather,
                        TimeSegment = scenario.TimeSegment };
                    // Let the initial two-second alignment finish before advancing the test clock.
                    var playback = new DebugPlayback(snapshot, end, command.speed,
                        command.smoothTransitions, realtime + 3);
                    Playback = playback;
                    _testCycle = cycle;
                    _testWindow = window;
                    _frames = _over50ms = 0;
                    _frameTotal = _frameMax = 0;
                    _gcStart = GC.CollectionCount(0);
                    _measureAfter = realtime + 3;
                    Array.Clear(_frameHistogram, 0, _frameHistogram.Length);
                    // A new run explicitly returns ownership from a prior manual test.
                    GameEnvironmentRuntime.Shared.Transitions.Dispose();
                }
                else if (command.action == "test_stop")
                {
                    StopPlayback();
                }
                else
                {
                    if (Playback == null) return SerializeError("没有活动测试，请先 test_start");
                    switch (command.action)
                    {
                        case "test_pause": Playback.SetSpeed(0, realtime); break;
                        case "test_resume": Playback.Resume(realtime); break;
                        case "test_speed": Playback.SetSpeed(command.speed, realtime); break;
                        case "test_seek":
                            if (double.IsNaN(command.progress) || command.progress < 0 || command.progress > 1)
                                return SerializeError("progress 必须介于 0 和 1");
                            Playback.Seek(_testCycle
                                ? Playback.Inputs.Now.AddSeconds((Playback.End - Playback.Inputs.Now).TotalSeconds * command.progress)
                                : _testWindow.AtProgress(command.progress), realtime);
                            break;
                        default: return SerializeError("未知测试操作");
                    }
                }
                TriggerImmediateApplication();
                return CaptureSnapshot();
            }
            catch (ArgumentException ex) { return SerializeError(ex.Message); }
        }

        internal static void TickPlayback()
        {
            if (Playback == null) return;
            double realtime = Time.realtimeSinceStartupAsDouble;
            if (realtime >= _measureAfter && Playback.Now(realtime) < Playback.End && Playback.Speed > 0)
            {
                double ms = Time.unscaledDeltaTime * 1000;
                _frames++;
                _frameTotal += ms;
                _frameMax = Math.Max(_frameMax, ms);
                if (ms > 50) _over50ms++;
                _frameHistogram[Math.Min(1000, (int)Math.Ceiling(ms))]++;
            }
            if (!Playback.Smooth && !ChillEnvPlugin.IsInCutscene() && !SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                var target = ModEnvironmentMap.ToGame(EnvironmentDecision.Resolve(BuildPolicy(), ModEnvironmentContext.Capture()).Base);
                if (GameEnvironmentRuntime.Shared.TryIsWindowActive(target, out bool active) && !active)
                    GameEnvironmentRuntime.Shared.TryApplyBaseImmediately(target, EnvironmentApplyMode.Interactive);
            }
        }

        private static int FrameP95()
        {
            int total = 0;
            if (_frames == 0) return 0;
            for (int i = 0; i < _frameHistogram.Length; i++)
            {
                total += _frameHistogram[i];
                if (total >= _frames * .95) return i;
            }
            return 1000;
        }

        public static string ApplyScenario(
            int weatherCode,
            int temperature,
            string weatherText,
            string currentDateTime,
            string sunrise,
            string sunset)
        {
            if (!IsConfigurationReady())
            {
                return SerializeError("Mod 配置尚未初始化");
            }

            var input = new DebugScenarioInput
            {
                WeatherCode = weatherCode,
                Temperature = temperature,
                WeatherText = weatherText,
                CurrentDateTime = currentDateTime,
                Sunrise = sunrise,
                Sunset = sunset
            };
            if (!DebugScenario.TryCreate(input, DateTime.Now, out _, out var error))
            {
                return SerializeError(error);
            }

            if (!StopPlayback()) GameEnvironmentRuntime.Shared.Transitions.Dispose();
            ChillEnvPlugin.Cfg_DebugCode.Value = weatherCode;
            ChillEnvPlugin.Cfg_DebugTemp.Value = temperature;
            ChillEnvPlugin.Cfg_DebugText.Value = string.IsNullOrWhiteSpace(weatherText)
                ? "DebugWeather"
                : weatherText.Trim();
            ChillEnvPlugin.Cfg_DebugCurrentDateTime.Value = currentDateTime?.Trim() ?? string.Empty;
            ChillEnvPlugin.Cfg_DebugSunriseTime.Value = sunrise.Trim();
            ChillEnvPlugin.Cfg_DebugSunsetTime.Value = sunset.Trim();
            ChillEnvPlugin.Cfg_DebugMode.Value = true;
            ChillEnvPlugin.Instance.Config.Save();

            ChillEnvPlugin.Log?.LogInfo(
                $"[调试场景] 已应用: code={weatherCode}, temp={temperature}, " +
                $"time={currentDateTime}, sunrise={sunrise}, sunset={sunset}");
            TriggerImmediateApplication();
            return CaptureSnapshot();
        }

        public static string DisableScenario()
        {
            if (!IsConfigurationReady())
            {
                return SerializeError("Mod 配置尚未初始化");
            }

            bool wasDebug = ChillEnvPlugin.Cfg_DebugMode.Value;
            if (!StopPlayback() && wasDebug) GameEnvironmentRuntime.Shared.Transitions.Dispose();
            ChillEnvPlugin.Cfg_DebugMode.Value = false;
            ChillEnvPlugin.Instance.Config.Save();
            ChillEnvPlugin.Log?.LogInfo("[调试场景] 已关闭，恢复真实输入");
            TriggerImmediateApplication();
            return CaptureSnapshot();
        }

        private static bool StopPlayback()
        {
            if (Playback == null) return false;
            Playback = null;
            // Holds belong to the calendar on which they were acquired.
            GameEnvironmentRuntime.Shared.Transitions.Dispose();
            return true;
        }

        public static string CaptureSnapshot()
        {
            if (!IsConfigurationReady())
            {
                return SerializeError("Mod 配置尚未初始化");
            }

            EnvironmentInputSnapshot inputs = ModEnvironmentContext.Capture();
            SyncPolicySnapshot policy = BuildPolicy();
            // Expected state resolves through the same module the runner applies
            // from, so this check can never validate against a stale copy of the rule.
            EnvironmentDecisionResult decision = EnvironmentDecision.Resolve(policy, inputs, GameEnvironmentRuntime.Shared.Transitions.UsesContinuousPresentation);
            EnvironmentType? expectedBase = policy.CanControlTime
                ? ModEnvironmentMap.ToGame(decision.Base)
                : (EnvironmentType?)null;
            EnvironmentType? expectedScenery = ModEnvironmentMap.ToGame(decision.Scenery);

            bool runtimeReady = GameEnvironmentRuntime.Shared.TryPrepare();
            var actualStates = new Dictionary<EnvironmentType, bool>();
            var activeEnvironments = new List<string>();
            bool actualStateReadable = true;
            foreach (EnvironmentType environmentType in BaseEnvironments)
            {
                actualStateReadable &= ReadActualState(
                    environmentType,
                    actualStates,
                    activeEnvironments);
            }
            foreach (EnvironmentType environmentType in SceneryWeathers)
            {
                actualStateReadable &= ReadActualState(
                    environmentType,
                    actualStates,
                    activeEnvironments);
            }

            bool baseMatches = !policy.CanControlTime ||
                (expectedBase.HasValue &&
                 IsOnlyExpectedActive(expectedBase.Value, BaseEnvironments, actualStates));
            bool sceneryMatches = !policy.CanControlWeather || inputs.Weather == null ||
                IsOnlyExpectedSceneryActive(expectedScenery, actualStates);

            var snapshot = new DebugRuntimeSnapshot
            {
                success = true,
                debugEnabled = inputs.IsDebug,
                configurationValid = inputs.ConfigurationValid,
                configurationError = inputs.ConfigurationError ?? string.Empty,
                effectiveDateTime = inputs.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sunrise = inputs.Sunrise.ToString(@"hh\:mm"),
                sunset = inputs.Sunset.ToString(@"hh\:mm"),
                weatherCode = inputs.Weather?.Code ?? -1,
                temperature = inputs.Weather?.Temperature ?? 0,
                weatherText = inputs.Weather?.Text ?? string.Empty,
                timeSyncEnabled = policy.CanControlTime,
                weatherSyncEnabled = policy.CanControlWeather,
                expectedBase = policy.CanControlTime
                    ? expectedBase?.ToString() ?? "unknown"
                    : "unmanaged",
                expectedScenery = policy.CanControlWeather
                    ? expectedScenery?.ToString() ?? "none"
                    : "unmanaged",
                actualActive = activeEnvironments.Count == 0
                    ? "none"
                    : string.Join(",", activeEnvironments.ToArray()),
                actualStateReadable = actualStateReadable,
                runtimeReady = runtimeReady,
                registeredControllers = GameEnvironmentRuntime.Shared.RegisteredControllerCount,
                pluginInitialized = ChillEnvPlugin.Initialized,
                inCutscene = ChillEnvPlugin.IsInCutscene(),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                lastApplied = AutoEnvRunner.LastAppliedEnvironment?.ToString() ?? "none",
                uiWeatherText = ChillEnvPlugin.UIWeatherString ?? string.Empty,
                whaleOverrideActive = SceneryAutomationSystem.IsWhaleSystemTriggered,
                smoothTransitionsEnabled = SmoothEnabled,
                testActive = Playback != null,
                testCycle = Playback != null && _testCycle,
                testSpeed = Playback?.Speed ?? 0,
                testComplete = Playback != null && inputs.Now >= Playback.End,
                testWindowProgress = Playback == null ? 0 : _testCycle ? -1 : _testWindow.Progress(inputs.Now),
                testWindowDurationSeconds = Playback == null || _testCycle ? 0 : _testWindow.DurationSeconds,
                testTimelineProgress = Playback == null ? 0 : Math.Max(0, Math.Min(1,
                    (inputs.Now - Playback.Inputs.Now).TotalSeconds / (Playback.End - Playback.Inputs.Now).TotalSeconds)),
                testFrameCount = _frames,
                testAverageFrameMs = _frames == 0 ? 0 : _frameTotal / _frames,
                testMaxFrameMs = _frameMax,
                testP95FrameMs = FrameP95(),
                testFramesOver50ms = _over50ms,
                testGen0Collections = Playback == null ? 0 : GC.CollectionCount(0) - _gcStart,
                transitionMode = GameEnvironmentRuntime.Shared.Transitions.Mode,
                transitionFrom = GameEnvironmentRuntime.Shared.Transitions.Current.From.ToString(),
                transitionTo = GameEnvironmentRuntime.Shared.Transitions.Current.To.ToString(),
                transitionProgress = GameEnvironmentRuntime.Shared.Transitions.Current.Amount,
                transitionManualUntil = GameEnvironmentRuntime.Shared.Transitions.ManualUntil?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                transitionUnavailableReason = GameEnvironmentRuntime.Shared.Transitions.UnavailableReason,
                continuousPresentation = GameEnvironmentRuntime.Shared.Transitions.UsesContinuousPresentation,
                presentationOwned = GameEnvironmentRuntime.Shared.Transitions.OwnsPresentation,
                daylightProgress = GameEnvironmentRuntime.Shared.Transitions.Daylight.Progress,
                daylightStrength = GameEnvironmentRuntime.Shared.Transitions.Daylight.Strength,
                solarPhase = GameEnvironmentRuntime.Shared.Transitions.Daylight.SolarPhase,
                targetCloudWeight = GameEnvironmentRuntime.Shared.Transitions.DaylightTarget.Clouds,
                actualCloudWeight = GameEnvironmentRuntime.Shared.Transitions.Daylight.Clouds,
                targetDimming = GameEnvironmentRuntime.Shared.Transitions.DaylightTarget.Weather.Dimming,
                actualDimming = GameEnvironmentRuntime.Shared.Transitions.Daylight.Weather.Dimming,
                targetFog = GameEnvironmentRuntime.Shared.Transitions.DaylightTarget.Weather.Fog,
                actualFog = GameEnvironmentRuntime.Shared.Transitions.Daylight.Weather.Fog,
                targetRain = GameEnvironmentRuntime.Shared.Precipitation.Target.Rain,
                actualRain = GameEnvironmentRuntime.Shared.Precipitation.Current.Rain,
                targetThunder = GameEnvironmentRuntime.Shared.Precipitation.Target.Thunder,
                actualThunder = GameEnvironmentRuntime.Shared.Precipitation.Current.Thunder,
                rainFadingOut = GameEnvironmentRuntime.Shared.Precipitation.FadingOut?.ToString() ?? "",
                matchesExpected = inputs.ConfigurationValid &&
                    runtimeReady &&
                    actualStateReadable &&
                    baseMatches &&
                    sceneryMatches &&
                    !SceneryAutomationSystem.IsWhaleSystemTriggered
            };
            return JsonUtility.ToJson(snapshot);
        }

        private static void TriggerImmediateApplication()
        {
            AutoEnvRunner.TriggerDebugSync();
            SceneryAutomationSystem.RequestImmediateAutomationCheck();
        }

        private static bool IsConfigurationReady()
        {
            // UnityEngine.Object overloads ==/!= and can report a BepInEx plugin
            // component as destroyed even though its managed instance and Config
            // remain available after the scene transition.
            return !ReferenceEquals(ChillEnvPlugin.Instance, null) &&
                ChillEnvPlugin.Cfg_DebugMode != null &&
                ChillEnvPlugin.Cfg_DebugCurrentDateTime != null &&
                ChillEnvPlugin.Cfg_DebugSunriseTime != null &&
                ChillEnvPlugin.Cfg_DebugSunsetTime != null;
        }

        private static bool ReadActualState(
            EnvironmentType environmentType,
            Dictionary<EnvironmentType, bool> actualStates,
            List<string> activeEnvironments)
        {
            // Main weather/base effects are window views. Some (notably Snow)
            // have an ambient behaviour object without a readable ambient enum,
            // so the combined audio probe can report false while the visual
            // effect is correctly active. Use the same window-state truth source
            // as AutoEnvRunner's startup verification.
            if (!GameEnvironmentRuntime.Shared.TryIsWindowActive(
                    environmentType,
                    out var isActive))
            {
                actualStates[environmentType] = false;
                return false;
            }

            actualStates[environmentType] = isActive;
            if (isActive)
            {
                activeEnvironments.Add(environmentType.ToString());
            }

            return true;
        }

        private static bool IsOnlyExpectedActive(
            EnvironmentType expected,
            EnvironmentType[] candidates,
            Dictionary<EnvironmentType, bool> actualStates)
        {
            foreach (EnvironmentType candidate in candidates)
            {
                bool shouldBeActive = candidate == expected;
                if (!actualStates.TryGetValue(candidate, out var isActive) ||
                    isActive != shouldBeActive)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsOnlyExpectedSceneryActive(
            EnvironmentType? expected,
            Dictionary<EnvironmentType, bool> actualStates)
        {
            foreach (EnvironmentType candidate in SceneryWeathers)
            {
                bool shouldBeActive = expected.HasValue && candidate == expected.Value;
                if (!actualStates.TryGetValue(candidate, out var isActive) ||
                    isActive != shouldBeActive)
                {
                    return false;
                }
            }

            return true;
        }

        private static string SerializeError(string error)
        {
            return JsonUtility.ToJson(new DebugCommandError
            {
                success = false,
                error = error ?? "未知调试错误"
            });
        }

        [Serializable]
        private sealed class DebugRuntimeSnapshot
        {
            public bool testActive;
            public bool testCycle;
            public bool testComplete;
            public double testSpeed;
            public double testWindowProgress;
            public double testWindowDurationSeconds;
            public double testTimelineProgress;
            public int testFrameCount;
            public double testAverageFrameMs;
            public double testMaxFrameMs;
            public int testP95FrameMs;
            public int testFramesOver50ms;
            public int testGen0Collections;
            public bool success;
            public bool debugEnabled;
            public bool configurationValid;
            public string configurationError;
            public string effectiveDateTime;
            public string sunrise;
            public string sunset;
            public int weatherCode;
            public int temperature;
            public string weatherText;
            public bool timeSyncEnabled;
            public bool weatherSyncEnabled;
            public string expectedBase;
            public string expectedScenery;
            public string actualActive;
            public bool actualStateReadable;
            public bool runtimeReady;
            public int registeredControllers;
            public bool pluginInitialized;
            public bool inCutscene;
            public string scene;
            public string lastApplied;
            public string uiWeatherText;
            public bool whaleOverrideActive;
            public bool matchesExpected;
            public bool smoothTransitionsEnabled;
            public bool continuousPresentation;
            public bool presentationOwned;
            public float daylightProgress;
            public float daylightStrength;
            public float solarPhase;
            public float targetCloudWeight;
            public float actualCloudWeight;
            public float targetDimming;
            public float actualDimming;
            public float targetFog;
            public float actualFog;
            public float targetRain;
            public float actualRain;
            public float targetThunder;
            public float actualThunder;
            public string rainFadingOut;
            public string transitionMode;
            public string transitionFrom;
            public string transitionTo;
            public float transitionProgress;
            public string transitionManualUntil;
            public string transitionUnavailableReason;
        }

        [Serializable]
        private sealed class PlaybackCommand
        {
            public string action = "test_start";
            public string boundary = "sunset";
            public int days = 2;
            public int weatherCode = 1;
            public int temperature = 20;
            public double speed = 20;
            public double progress = 0.5;
            public bool smoothTransitions = true;
        }

        [Serializable]
        private sealed class DebugCommandError
        {
            public bool success;
            public string error;
        }
    }
}
