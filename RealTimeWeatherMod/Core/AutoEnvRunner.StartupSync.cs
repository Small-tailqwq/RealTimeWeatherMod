using System;
using System.Reflection;
using Bulbul;
using HarmonyLib;
using UnityEngine;
using ChillWithYou.EnvSync.Models;
using ChillWithYou.EnvSync.Services;
using ChillWithYou.EnvSync.Utils;

namespace ChillWithYou.EnvSync.Core
{
    public partial class AutoEnvRunner
    {
        private const float StartupSyncPollIntervalSeconds = 0.25f;
        private const float EnvironmentUiRescanIntervalSeconds = 1f;

        private bool _firstSyncDone;
        private bool _startupSyncRunning;
        private bool _startupAppliedOnce;
        private bool _startupWeatherFetchFinished;
        private float _nextEnvironmentUiScanTime;
        private WeatherInfo _pendingStartupWeather;
        private MonoBehaviour _cachedEnvironmentUi;
        private Type _cachedEnvironmentUiType;
        private FieldInfo _cachedWindowViewServiceField;
        private FieldInfo _cachedEnvironmentDataServiceField;
        private MethodInfo _cachedChangeTimeMethod;
        private Type _cachedChangeTimeEnumType;
        private Type _cachedWindowViewServiceType;
        private Type _cachedEnvironmentDataServiceType;
        private MethodInfo _cachedActivateWindowMethod;
        private MethodInfo _cachedDeactivateWindowMethod;
        private MethodInfo _cachedSetViewActiveMethod;
        private MethodInfo _cachedIsMuteMethod;
        private MethodInfo _cachedSetMuteMethod;
        private Type _cachedWindowViewEnumType;
        private Type _cachedAmbientSoundEnumType;

        private MonoBehaviour FindEnvironmentUI()
        {
            if (IsUsableEnvironmentUI(_cachedEnvironmentUi))
            {
                return _cachedEnvironmentUi;
            }

            if (Time.time < _nextEnvironmentUiScanTime)
            {
                return null;
            }

            _nextEnvironmentUiScanTime = Time.time + EnvironmentUiRescanIntervalSeconds;

            Type uiType = AccessTools.TypeByName("Bulbul.EnvironmentUI");
            if (uiType == null)
            {
                return null;
            }

            var allUIs = Resources.FindObjectsOfTypeAll(uiType);
            if (allUIs == null)
            {
                return null;
            }

            foreach (var obj in allUIs)
            {
                var mono = obj as MonoBehaviour;
                if (IsUsableEnvironmentUI(mono))
                {
                    _cachedEnvironmentUi = mono;
                    return mono;
                }
            }

            return null;
        }

        private static bool IsUsableEnvironmentUI(MonoBehaviour envUI)
        {
            return envUI != null && envUI.gameObject != null && envUI.gameObject.scene.rootCount != 0;
        }

        private bool EnsureEnvironmentUiMetadata(MonoBehaviour envUI)
        {
            Type envUiType = envUI.GetType();
            if (_cachedEnvironmentUiType == envUiType &&
                _cachedWindowViewServiceField != null &&
                _cachedEnvironmentDataServiceField != null &&
                _cachedChangeTimeMethod != null &&
                _cachedChangeTimeEnumType != null)
            {
                return true;
            }

            _cachedEnvironmentUiType = envUiType;
            _cachedWindowViewServiceField = AccessTools.Field(envUiType, "_windowViewService");
            _cachedEnvironmentDataServiceField = AccessTools.Field(envUiType, "_environmentDataService");
            _cachedChangeTimeMethod = AccessTools.Method(envUiType, "ChangeTime");
            _cachedChangeTimeEnumType = GetSingleParameterType(_cachedChangeTimeMethod);

            return _cachedWindowViewServiceField != null &&
                _cachedEnvironmentDataServiceField != null &&
                _cachedChangeTimeMethod != null &&
                _cachedChangeTimeEnumType != null;
        }

        private bool EnsureStartupSceneryMetadata(object windowViewService, object environmentDataService)
        {
            Type windowViewServiceType = windowViewService.GetType();
            Type environmentDataServiceType = environmentDataService.GetType();
            if (_cachedWindowViewServiceType == windowViewServiceType &&
                _cachedEnvironmentDataServiceType == environmentDataServiceType &&
                _cachedActivateWindowMethod != null &&
                _cachedDeactivateWindowMethod != null &&
                _cachedSetViewActiveMethod != null &&
                _cachedIsMuteMethod != null &&
                _cachedSetMuteMethod != null &&
                _cachedWindowViewEnumType != null &&
                _cachedAmbientSoundEnumType != null)
            {
                return true;
            }

            _cachedWindowViewServiceType = windowViewServiceType;
            _cachedEnvironmentDataServiceType = environmentDataServiceType;
            _cachedActivateWindowMethod = AccessTools.Method(windowViewServiceType, "ActivateWindow");
            _cachedDeactivateWindowMethod = AccessTools.Method(windowViewServiceType, "DeactivateWindow");
            _cachedSetViewActiveMethod = AccessTools.Method(environmentDataServiceType, "SetViewActive");
            _cachedIsMuteMethod = AccessTools.Method(environmentDataServiceType, "IsMute");
            _cachedSetMuteMethod = AccessTools.Method(environmentDataServiceType, "SetMute");
            _cachedWindowViewEnumType = GetSingleParameterType(_cachedActivateWindowMethod);
            _cachedAmbientSoundEnumType = GetSingleParameterType(_cachedIsMuteMethod);

            if (_cachedSetViewActiveMethod == null || _cachedSetMuteMethod == null)
            {
                return false;
            }

            var setViewActiveParameters = _cachedSetViewActiveMethod.GetParameters();
            var setMuteParameters = _cachedSetMuteMethod.GetParameters();
            return _cachedActivateWindowMethod != null &&
                _cachedDeactivateWindowMethod != null &&
                _cachedIsMuteMethod != null &&
                _cachedWindowViewEnumType != null &&
                _cachedAmbientSoundEnumType != null &&
                setViewActiveParameters.Length == 2 &&
                setViewActiveParameters[0].ParameterType == _cachedWindowViewEnumType &&
                setViewActiveParameters[1].ParameterType == typeof(bool) &&
                setMuteParameters.Length == 2 &&
                setMuteParameters[0].ParameterType == _cachedAmbientSoundEnumType &&
                setMuteParameters[1].ParameterType == typeof(bool);
        }

        private static Type GetSingleParameterType(MethodInfo method)
        {
            if (method == null)
            {
                return null;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return null;
            }

            return parameters[0].ParameterType;
        }

        private bool TryGetStartupRuntime(MonoBehaviour envUI, out object windowViewService, out object environmentDataService)
        {
            windowViewService = null;
            environmentDataService = null;

            if (!IsUsableEnvironmentUI(envUI) || !EnsureEnvironmentUiMetadata(envUI))
            {
                return false;
            }

            windowViewService = _cachedWindowViewServiceField.GetValue(envUI);
            environmentDataService = _cachedEnvironmentDataServiceField.GetValue(envUI);
            return windowViewService != null && environmentDataService != null;
        }

        private EnvironmentType GetDesiredBaseEnvironment(SyncPolicySnapshot policy, WeatherInfo weather)
        {
            EnvironmentType target = GetBaseTimeEnvironmentOnly();
            if (policy.CanApplyCloudyOverride && weather != null && IsBadWeather(weather.Code) && target != EnvironmentType.Night)
            {
                target = EnvironmentType.Cloudy;
            }

            return target;
        }

        private bool TryApplyStartupBase(MonoBehaviour envUI, EnvironmentType target)
        {
            if (_startupAppliedOnce && IsEnvironmentActive(target))
            {
                return true;
            }

            if (!EnsureEnvironmentUiMetadata(envUI))
            {
                return false;
            }

            object enumValue;
            if (!TryGetRuntimeEnumValue(_cachedChangeTimeEnumType, target.ToString(), out enumValue))
            {
                return false;
            }

            return TryInvokeRuntimeMethod(
                _cachedChangeTimeMethod,
                envUI,
                new[] { enumValue },
                "[启动同步] 时间基底尚未可用");
        }

        private bool TryApplyStartupScenery(
            object windowViewService,
            object environmentDataService,
            EnvironmentType? target)
        {
            // EnvironmentController.Setup happens after the character sits down.
            // Its underlying services are injected earlier, so use the same final pipeline during startup.
            if (!EnsureStartupSceneryMetadata(windowViewService, environmentDataService))
            {
                return false;
            }

            foreach (var env in SceneryWeathers)
            {
                bool shouldBeActive = target.HasValue && target.Value == env;

                if (EnvRegistry.TryGet(env, out var controller))
                {
                    bool isInTargetState;
                    if (EnvironmentControllerStateSetter.TryIsInTargetState(
                        controller,
                        shouldBeActive,
                        out isInTargetState))
                    {
                        if (isInTargetState ||
                            EnvironmentControllerStateSetter.TrySetCombinedState(controller, shouldBeActive))
                        {
                            continue;
                        }
                    }
                }

                bool isWindowActive = IsEnvironmentActive(env);
                object windowEnumValue;
                object ambientEnumValue;
                if (!TryGetRuntimeEnumValue(_cachedWindowViewEnumType, env.ToString(), out windowEnumValue))
                {
                    return false;
                }

                bool hasAmbientSound = TryGetRuntimeEnumValue(
                    _cachedAmbientSoundEnumType,
                    env.ToString(),
                    out ambientEnumValue);
                bool isSoundActive = false;
                if (hasAmbientSound &&
                    !TryGetStartupSoundActive(environmentDataService, ambientEnumValue, out isSoundActive))
                {
                    ChillEnvPlugin.Log?.LogWarning("[启动同步] 无法获取降水音频状态，跳过音频同步");
                    hasAmbientSound = false;
                }

                if (isWindowActive != shouldBeActive)
                {
                    var changeMethod = shouldBeActive ? _cachedActivateWindowMethod : _cachedDeactivateWindowMethod;
                    if (!TryInvokeRuntimeMethod(
                        changeMethod,
                        windowViewService,
                        new[] { windowEnumValue },
                        "[启动同步] 降水窗景直连尚未可用") ||
                        !TryInvokeRuntimeMethod(
                            _cachedSetViewActiveMethod,
                            environmentDataService,
                            new object[] { windowEnumValue, shouldBeActive },
                            "[启动同步] 降水窗景状态尚未可用"))
                    {
                        return false;
                    }
                }

                if (hasAmbientSound &&
                    isSoundActive != shouldBeActive)
                {
                    if (!TryInvokeRuntimeMethod(
                        _cachedSetMuteMethod,
                        environmentDataService,
                        new object[] { ambientEnumValue, !shouldBeActive },
                        "[启动同步] 降水音频状态尚未可用"))
                    {
                        ChillEnvPlugin.Log?.LogWarning("[启动同步] 设置降水音频静音状态失败");
                    }
                }
            }

            return true;
        }

        private bool TryGetStartupSoundActive(
            object environmentDataService,
            object ambientEnumValue,
            out bool isActive)
        {
            isActive = false;

            try
            {
                isActive = !(bool)_cachedIsMuteMethod.Invoke(
                    environmentDataService,
                    new[] { ambientEnumValue });
                return true;
            }
            catch (ArgumentException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"[启动同步] 无法读取降水音频状态: {ex.Message}");
            }
            catch (TargetException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"[启动同步] 无法读取降水音频状态: {ex.Message}");
            }
            catch (MethodAccessException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"[启动同步] 无法读取降水音频状态: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                ChillEnvPlugin.Log?.LogDebug(
                    $"[启动同步] 无法读取降水音频状态: {GetInvocationMessage(ex)}");
            }

            return false;
        }

        private static bool TryGetRuntimeEnumValue(Type enumType, string valueName, out object enumValue)
        {
            enumValue = null;
            if (enumType == null || !enumType.IsEnum || !Enum.IsDefined(enumType, valueName))
            {
                return false;
            }

            enumValue = Enum.Parse(enumType, valueName);
            return true;
        }

        private bool TryInvokeRuntimeMethod(MethodInfo method, object target, object[] arguments, string logPrefix)
        {
            try
            {
                method.Invoke(target, arguments);
                return true;
            }
            catch (ArgumentException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"{logPrefix}: {ex.Message}");
            }
            catch (TargetException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"{logPrefix}: {ex.Message}");
            }
            catch (MethodAccessException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"{logPrefix}: {ex.Message}");
            }
            catch (TargetInvocationException ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"{logPrefix}: {GetInvocationMessage(ex)}");
            }

            return false;
        }

        private static string GetInvocationMessage(TargetInvocationException ex)
        {
            return ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        }

        private bool AreSceneryControllersReady()
        {
            foreach (var env in SceneryWeathers)
            {
                if (!EnvRegistry.TryGet(env, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsStartupStateApplied(SyncPolicySnapshot policy, WeatherInfo weather)
        {
            if (policy.CanControlTime && !IsEnvironmentActive(GetDesiredBaseEnvironment(policy, weather)))
            {
                return false;
            }

            if (policy.CanControlWeather && weather != null)
            {
                EnvironmentType? target = GetSceneryType(weather.Code);
                foreach (var env in SceneryWeathers)
                {
                    bool shouldBeActive = target.HasValue && target.Value == env;
                    if (IsEnvironmentActive(env) != shouldBeActive)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryApplyStartupEnvironment(
            SyncPolicySnapshot policy,
            WeatherInfo weather,
            MonoBehaviour envUI,
            object windowViewService,
            object environmentDataService)
        {
            if (policy.CanControlTime && !TryApplyStartupBase(envUI, GetDesiredBaseEnvironment(policy, weather)))
            {
                return false;
            }

            if (policy.CanControlWeather && weather != null &&
                !TryApplyStartupScenery(windowViewService, environmentDataService, GetSceneryType(weather.Code)))
            {
                return false;
            }

            return IsStartupStateApplied(policy, weather);
        }

        private System.Collections.IEnumerator EarlyStartupSync()
        {
            if (_startupSyncRunning || _firstSyncDone)
            {
                yield break;
            }

            _startupSyncRunning = true;
            var policy = BuildPolicySnapshot();
            bool hasApiKey = HasUsableApiKey();
            bool needsWeatherData = policy.CanControlWeather && hasApiKey;
            bool needWeatherFetch = (policy.CanControlWeather || policy.CanFetchWeatherForUI) && hasApiKey;
            string location = ChillEnvPlugin.Cfg_Location.Value;

            if (WeatherService.HasValidCache(location))
            {
                _pendingStartupWeather = WeatherService.CachedWeather;
                _startupWeatherFetchFinished = true;
                UpdateUiWeatherString(_pendingStartupWeather);
                CheckAndSyncSunSchedule();
            }
            else if (needWeatherFetch)
            {
                _startupWeatherFetchFinished = false;
                StartCoroutine(WeatherService.FetchWeather(
                    ChillEnvPlugin.Cfg_SeniverseKey.Value,
                    location,
                    false,
                    (weather) =>
                    {
                        _startupWeatherFetchFinished = true;
                        if (weather != null)
                        {
                            _pendingStartupWeather = weather;
                            UpdateUiWeatherString(weather);
                        }
                        CheckAndSyncSunSchedule();
                    }));
            }
            else
            {
                _startupWeatherFetchFinished = true;
                CheckAndSyncSunSchedule();
            }

            float timeout = 30f;
            while (!_firstSyncDone && timeout > 0f)
            {
                WeatherInfo weather = _pendingStartupWeather;
                MonoBehaviour envUI = FindEnvironmentUI();
                object windowViewService;
                object environmentDataService;
                bool runtimeReady = TryGetStartupRuntime(envUI, out windowViewService, out environmentDataService);
                bool weatherDecisionReady = weather != null || _startupWeatherFetchFinished;
                bool needsSceneryControllers = policy.CanControlWeather && weather != null;
                bool sceneryControllersReady = !needsSceneryControllers || AreSceneryControllersReady();

                var action = StartupWeatherSyncPolicy.Determine(
                    policy.CanMutateEnvironment,
                    runtimeReady,
                    needsWeatherData,
                    weatherDecisionReady,
                    needsSceneryControllers,
                    sceneryControllersReady);

                if (action == StartupWeatherSyncAction.Skip)
                {
                    _firstSyncDone = true;
                    break;
                }

                if (action == StartupWeatherSyncAction.ApplyAndKeepSettling ||
                    action == StartupWeatherSyncAction.ApplyAndFinish)
                {
                    bool applied = TryApplyStartupEnvironment(
                        policy,
                        weather,
                        envUI,
                        windowViewService,
                        environmentDataService);

                    if (applied && !_startupAppliedOnce)
                    {
                        _startupAppliedOnce = true;
                        string scenery = weather == null ? "未接管" : (GetSceneryType(weather.Code)?.ToString() ?? "无降水");
                        string baseEnvironment = policy.CanControlTime ? GetDesiredBaseEnvironment(policy, weather).ToString() : "未接管";
                        ChillEnvPlugin.Log?.LogInfo($"[启动同步] 已在主角坐下前应用环境: base={baseEnvironment}, scenery={scenery}");
                    }

                    if (applied && action == StartupWeatherSyncAction.ApplyAndFinish)
                    {
                        _firstSyncDone = true;
                        ChillEnvPlugin.Log?.LogInfo("[启动同步] 环境状态验证完成");
                        break;
                    }
                }

                yield return new WaitForSeconds(StartupSyncPollIntervalSeconds);
                timeout -= StartupSyncPollIntervalSeconds;
            }

            if (!_firstSyncDone)
            {
                _firstSyncDone = _startupAppliedOnce;
                if (_startupAppliedOnce)
                {
                    ChillEnvPlugin.Log?.LogWarning("[启动同步] 控制器等待超时，但直连环境已应用并验证");
                }
                else
                {
                    ChillEnvPlugin.Log?.LogWarning("[启动同步] 超时等待环境运行时，后续由常规定时器兜底");
                }
            }

            _startupSyncRunning = false;
            if (hasApiKey)
            {
                ScheduleNextWeatherCheckFromCache(location);
            }
        }

        public static void TriggerImmediateSync()
        {
            if (_instance != null && !_instance._firstSyncDone && !_instance._startupSyncRunning)
            {
                _instance.StartCoroutine(_instance.EarlyStartupSync());
            }
        }
    }
}
