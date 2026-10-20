using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ChillWithYou.EnvSync.Core
{
    internal enum EnvironmentApplyMode
    {
        Interactive,
        EarlyStartup
    }

    /// <summary>
    /// Owns the fragile seam between the mod and the game's environment runtime.
    /// Callers describe the state they need; discovery, reflection, compatibility
    /// fallbacks and controller readiness stay inside this module.
    /// </summary>
    internal sealed class GameEnvironmentRuntime
    {
        private const float EnvironmentUiRescanIntervalSeconds = 1f;

        internal static GameEnvironmentRuntime Shared { get; } = new GameEnvironmentRuntime();

        private readonly Dictionary<EnvironmentType, EnvironmentController> _controllers =
            new Dictionary<EnvironmentType, EnvironmentController>();

        private MonoBehaviour _environmentUi;
        private float _nextEnvironmentUiScanTime;
        private Type _environmentUiType;
        private FieldInfo _windowViewServiceField;
        private FieldInfo _environmentDataServiceField;
        private MethodInfo _changeTimeMethod;
        private MethodInfo _clickChangeTimeMethod;
        private Type _changeTimeEnumType;
        private Type _clickChangeTimeEnumType;

        private Type _windowViewServiceType;
        private Type _environmentDataServiceType;
        private MethodInfo _activateWindowMethod;
        private MethodInfo _deactivateWindowMethod;
        private MethodInfo _setViewActiveMethod;
        private MethodInfo _isMuteMethod;
        private MethodInfo _setMuteMethod;
        private Type _windowViewEnumType;
        private Type _ambientSoundEnumType;

        private bool _loggedSaveDataFallbackOnce;

        private GameEnvironmentRuntime()
        {
            Transitions = new TimeTransitionController(this);
            Precipitation = new PrecipitationController(this);
        }

        internal TimeTransitionController Transitions { get; }
        internal PrecipitationController Precipitation { get; }
        internal int AutomaticApplyDepth { get; private set; }

        internal int RegisteredControllerCount
        {
            get
            {
                RemoveDestroyedControllers();
                return _controllers.Count;
            }
        }

        internal void Register(EnvironmentController controller)
        {
            if (controller != null)
            {
                _controllers[controller.EnvironmentType] = controller;
            }
        }

        internal bool HasController(EnvironmentType environmentType)
        {
            return TryGetController(environmentType, out _);
        }

        internal bool AreControllersReady(IEnumerable<EnvironmentType> environmentTypes)
        {
            foreach (var environmentType in environmentTypes)
            {
                if (!TryGetController(environmentType, out _))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryPrepare()
        {
            return TryGetRuntimeServices(out _, out _, out _);
        }

        internal bool TryApplyBase(EnvironmentType target, EnvironmentApplyMode mode)
        {
            return TryApplyBase(target, mode, out _);
        }

        internal bool TryApplyBase(EnvironmentType target, EnvironmentApplyMode mode, out bool appliedImmediately)
        {
            appliedImmediately = false;
            if (Transitions.TryAutomaticRequest(mode)) return true;
            appliedImmediately = TryApplyBaseImmediately(target, mode);
            return appliedImmediately;
        }

        internal bool TryApplyBaseImmediately(EnvironmentType target, EnvironmentApplyMode mode)
        {
            AutomaticApplyDepth++;
            try { return InvokeBase(target, mode); }
            finally { AutomaticApplyDepth--; }
        }

        private bool InvokeBase(EnvironmentType target, EnvironmentApplyMode mode)
        {
            MonoBehaviour environmentUi = FindEnvironmentUi();
            if (!IsUsableEnvironmentUi(environmentUi))
            {
                return false;
            }
            EnsureEnvironmentUiMetadata(environmentUi);

            MethodInfo method = mode == EnvironmentApplyMode.EarlyStartup
                ? _changeTimeMethod
                : _clickChangeTimeMethod;
            Type enumType = mode == EnvironmentApplyMode.EarlyStartup
                ? _changeTimeEnumType
                : _clickChangeTimeEnumType;

            if (!TryGetRuntimeEnumValue(enumType, target.ToString(), out var enumValue))
            {
                return false;
            }

            return TryInvoke(
                method,
                environmentUi,
                new[] { enumValue },
                mode == EnvironmentApplyMode.EarlyStartup
                    ? "[环境运行时] 启动期基底切换尚未可用"
                    : "[环境运行时] 基底切换失败");
        }

        internal bool TryApplySceneryState(
            EnvironmentType environmentType,
            bool targetState,
            EnvironmentApplyMode mode,
            bool applyPrecipitationSoundSetting = true)
        {
            bool mutePrecipitation = applyPrecipitationSoundSetting && IsPrecipitation(environmentType) &&
                !(ChillEnvPlugin.Cfg_EnablePrecipitationSounds?.Value ?? true);
            bool targetSoundState = targetState && !mutePrecipitation;
            if (TryGetController(environmentType, out var controller))
            {
                if (TryIsControllerInTargetState(controller, targetState, targetSoundState, out var isInTargetState))
                {
                    if (isInTargetState || TrySetControllerCombinedState(controller, targetState, targetSoundState))
                    {
                        return true;
                    }
                }

                if (mode == EnvironmentApplyMode.Interactive && !mutePrecipitation)
                {
                    return TrySimulateMainIconClick(environmentType);
                }
            }

            // The main icon toggles BOTH view and sound, so it cannot represent silent rain.
            // Use the direct window path and reconcile live sound if a controller exists.
            if (mode == EnvironmentApplyMode.EarlyStartup || mutePrecipitation)
            {
                bool windowApplied = TryApplySceneryStateDirect(environmentType, targetState, targetSoundState);
                bool soundApplied = !mutePrecipitation || TrySetAmbientSoundState(environmentType, false, out _);
                return windowApplied && soundApplied;
            }

            if (TryIsWindowActive(environmentType, out var isActive) && isActive == targetState)
            {
                return true;
            }

            return TrySimulateMainIconClick(environmentType);
        }

        internal void RefreshPrecipitationSounds()
        {
            if (!ChillEnvPlugin.Cfg_EnableWeatherSync.Value ||
                ChillEnvPlugin.IsInCutscene() || SceneryAutomationSystem.IsWhaleSystemTriggered)
            {
                return;
            }

            bool soundsEnabled = ChillEnvPlugin.Cfg_EnablePrecipitationSounds.Value;
            foreach (var environmentType in ModEnvironmentMap.SceneryEnvironments)
            {
                if (!IsPrecipitation(environmentType)) continue;

                // Preserve the current view, including rain that is still fading out.
                if (TryGetController(environmentType, out var controller) &&
                    TryGetControllerCombinedState(controller, out var windowActive, out _, out _, out _) &&
                    TrySetControllerCombinedState(controller, windowActive, windowActive && soundsEnabled))
                {
                    continue;
                }

                if (TryIsWindowActive(environmentType, out var fallbackWindowActive) &&
                    TrySetAmbientSoundState(environmentType, fallbackWindowActive && soundsEnabled, out _))
                {
                    continue;
                }

                ChillEnvPlugin.Log?.LogWarning($"[环境运行时] 无法立即更新降水声音: {environmentType}");
            }
        }

        private static bool IsPrecipitation(EnvironmentType environmentType)
        {
            return environmentType == EnvironmentType.LightRain ||
                environmentType == EnvironmentType.HeavyRain ||
                environmentType == EnvironmentType.ThunderRain;
        }

        internal ModEnvironment? GetActiveRain()
        {
            if (TryIsWindowActive(EnvironmentType.ThunderRain, out var thunder) && thunder)
                return ModEnvironment.ThunderRain;
            if (TryIsWindowActive(EnvironmentType.HeavyRain, out var heavy) && heavy)
                return ModEnvironment.HeavyRain;
            if (TryIsWindowActive(EnvironmentType.LightRain, out var light) && light)
                return ModEnvironment.LightRain;
            return null;
        }

        internal bool TryIsWindowActive(EnvironmentType environmentType, out bool isActive)
        {
            isActive = false;
            if (!Enum.TryParse(environmentType.ToString(), out WindowViewType windowViewType))
            {
                return false;
            }

            return TryIsWindowViewActive(windowViewType, out isActive);
        }

        internal bool TryIsActive(EnvironmentType environmentType, out bool isActive)
        {
            isActive = false;
            if (!TryGetController(environmentType, out var controller))
            {
                return TryIsWindowActive(environmentType, out isActive);
            }

            try
            {
                Type controllerType = controller.GetType();
                FieldInfo ambientBehaviorField = AccessTools.Field(controllerType, "_ambientSoundBehavior");
                if (ambientBehaviorField != null)
                {
                    object ambientBehavior = ambientBehaviorField.GetValue(controller);
                    if (ambientBehavior == null)
                    {
                        return true;
                    }

                    FieldInfo sliderField = AccessTools.Field(ambientBehavior.GetType(), "_volumeSlider");
                    var slider = sliderField?.GetValue(ambientBehavior) as Slider;
                    if (slider != null)
                    {
                        isActive = slider.value > 0f;
                        return true;
                    }

                    PropertyInfo behaviorActiveProperty = AccessTools.Property(ambientBehavior.GetType(), "IsActive");
                    if (behaviorActiveProperty != null)
                    {
                        isActive = (bool)behaviorActiveProperty.GetValue(ambientBehavior, null);
                        return true;
                    }

                    FieldInfo behaviorActiveField = AccessTools.Field(ambientBehavior.GetType(), "_isActive");
                    if (behaviorActiveField != null)
                    {
                        isActive = (bool)behaviorActiveField.GetValue(ambientBehavior);
                        return true;
                    }

                    return false;
                }

                FieldInfo windowBehaviorField = AccessTools.Field(controllerType, "_windowBehavior");
                object windowBehavior = windowBehaviorField?.GetValue(controller);
                PropertyInfo windowTypeProperty = windowBehavior == null
                    ? null
                    : AccessTools.Property(windowBehavior.GetType(), "WindowViewType");
                if (windowTypeProperty != null)
                {
                    var windowViewType = (WindowViewType)windowTypeProperty.GetValue(windowBehavior, null);
                    if (TryIsWindowViewActive(windowViewType, out isActive))
                    {
                        return true;
                    }
                }

                PropertyInfo activeProperty = AccessTools.Property(controllerType, "IsActive");
                if (activeProperty != null)
                {
                    isActive = (bool)activeProperty.GetValue(controller, null);
                    return true;
                }

                FieldInfo activeField = AccessTools.Field(controllerType, "_isActive");
                if (activeField != null)
                {
                    isActive = (bool)activeField.GetValue(controller);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning(
                    $"[环境运行时] 读取 {environmentType} 状态失败: {GetInvocationMessage(ex)}");
            }

            return false;
        }

        internal bool TryIsAmbientSoundActive(EnvironmentType environmentType, out bool isActive)
        {
            isActive = false;
            if (!Enum.TryParse(environmentType.ToString(), out AmbientSoundType ambientSoundType))
            {
                return false;
            }

            if (TryGetRuntimeServices(out _, out _, out var environmentDataServiceObject) &&
                environmentDataServiceObject is EnvironmentDataService environmentDataService)
            {
                try
                {
                    isActive = !environmentDataService.IsMuteOrVolumeZero(ambientSoundType);
                    return true;
                }
                catch (Exception ex)
                {
                    ChillEnvPlugin.Log?.LogDebug(
                        $"[环境运行时] 读取环境音 {environmentType} 失败: {GetInvocationMessage(ex)}");
                }
            }

            return TryIsActive(environmentType, out isActive);
        }

        internal bool TrySetAmbientSoundState(
            EnvironmentType environmentType,
            bool targetState,
            out bool changed)
        {
            changed = false;
            if (!Enum.TryParse(environmentType.ToString(), out AmbientSoundType ambientSoundType))
            {
                return false;
            }

            bool storedStateAvailable = false;
            if (TryGetRuntimeServices(out _, out _, out var environmentDataServiceObject) &&
                environmentDataServiceObject is EnvironmentDataService environmentDataService)
            {
                try
                {
                    storedStateAvailable = true;
                    bool shouldBeMuted = !targetState;
                    if (environmentDataService.IsMute(ambientSoundType) != shouldBeMuted)
                    {
                        environmentDataService.SetMute(ambientSoundType, shouldBeMuted);
                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    storedStateAvailable = false;
                    ChillEnvPlugin.Log?.LogWarning(
                        $"[环境运行时] 写入环境音 {environmentType} 状态失败: {GetInvocationMessage(ex)}");
                }
            }

            bool liveStateAvailable = false;
            if (TryGetController(environmentType, out var controller))
            {
                if (TryIsActive(environmentType, out var liveState) && liveState == targetState)
                {
                    liveStateAvailable = true;
                }
                else
                {
                    string methodName = targetState ? "MuteDeactivate" : "MuteActivate";
                    MethodInfo method = AccessTools.Method(controller.GetType(), methodName);
                    liveStateAvailable = TryInvoke(
                        method,
                        controller,
                        null,
                        $"[环境运行时] 环境音 {environmentType} 直连失败");
                    changed |= liveStateAvailable;
                }
            }

            return storedStateAvailable || liveStateAvailable;
        }

        internal bool TrySimulateMainIconClick(EnvironmentType environmentType)
        {
            if (!TryGetController(environmentType, out var controller))
            {
                return false;
            }

            MethodInfo clickMethod = AccessTools.Method(controller.GetType(), "OnClickButtonMainIcon");
            if (clickMethod == null)
            {
                ChillEnvPlugin.Log?.LogWarning(
                    $"[环境运行时] 未找到 OnClickButtonMainIcon: {environmentType}");
                return false;
            }

            Patches.UserInteractionPatch.IsSimulatingClick = true;
            try
            {
                return TryInvoke(
                    clickMethod,
                    controller,
                    null,
                    $"[环境运行时] 模拟点击失败: {environmentType}");
            }
            finally
            {
                Patches.UserInteractionPatch.IsSimulatingClick = false;
            }
        }

        private bool TryGetController(
            EnvironmentType environmentType,
            out EnvironmentController controller)
        {
            if (_controllers.TryGetValue(environmentType, out controller) && controller != null)
            {
                return true;
            }

            _controllers.Remove(environmentType);
            controller = null;
            return false;
        }

        private void RemoveDestroyedControllers()
        {
            List<EnvironmentType> destroyed = null;
            foreach (var pair in _controllers)
            {
                if (pair.Value != null)
                {
                    continue;
                }

                if (destroyed == null)
                {
                    destroyed = new List<EnvironmentType>();
                }
                destroyed.Add(pair.Key);
            }

            if (destroyed == null)
            {
                return;
            }

            foreach (var environmentType in destroyed)
            {
                _controllers.Remove(environmentType);
            }
        }

        private MonoBehaviour FindEnvironmentUi()
        {
            if (IsUsableEnvironmentUi(_environmentUi))
            {
                return _environmentUi;
            }

            if (Time.time < _nextEnvironmentUiScanTime)
            {
                return null;
            }

            _nextEnvironmentUiScanTime = Time.time + EnvironmentUiRescanIntervalSeconds;
            Type environmentUiType = AccessTools.TypeByName("Bulbul.EnvironmentUI");
            if (environmentUiType == null)
            {
                return null;
            }

            var environmentUis = Resources.FindObjectsOfTypeAll(environmentUiType);
            if (environmentUis == null)
            {
                return null;
            }

            foreach (var candidate in environmentUis)
            {
                var monoBehaviour = candidate as MonoBehaviour;
                if (!IsUsableEnvironmentUi(monoBehaviour))
                {
                    continue;
                }

                _environmentUi = monoBehaviour;
                return monoBehaviour;
            }

            return null;
        }

        private static bool IsUsableEnvironmentUi(MonoBehaviour environmentUi)
        {
            return environmentUi != null &&
                environmentUi.gameObject != null &&
                environmentUi.gameObject.scene.rootCount != 0;
        }

        private bool EnsureEnvironmentUiMetadata(MonoBehaviour environmentUi)
        {
            Type environmentUiType = environmentUi.GetType();
            if (_environmentUiType == environmentUiType)
            {
                return _windowViewServiceField != null && _environmentDataServiceField != null;
            }

            _environmentUiType = environmentUiType;
            _windowViewServiceField = AccessTools.Field(environmentUiType, "_windowViewService");
            _environmentDataServiceField = AccessTools.Field(environmentUiType, "_environmentDataService");
            _changeTimeMethod = AccessTools.Method(environmentUiType, "ChangeTime");
            _clickChangeTimeMethod = AccessTools.Method(environmentUiType, "OnClickButtonChangeTime");
            _changeTimeEnumType = GetSingleParameterType(_changeTimeMethod);
            _clickChangeTimeEnumType = GetSingleParameterType(_clickChangeTimeMethod);

            return _windowViewServiceField != null && _environmentDataServiceField != null;
        }

        internal bool TryGetRuntimeServices(
            out MonoBehaviour environmentUi,
            out object windowViewService,
            out object environmentDataService)
        {
            environmentUi = FindEnvironmentUi();
            windowViewService = null;
            environmentDataService = null;
            if (!IsUsableEnvironmentUi(environmentUi) || !EnsureEnvironmentUiMetadata(environmentUi))
            {
                return false;
            }

            try
            {
                windowViewService = _windowViewServiceField.GetValue(environmentUi);
                environmentDataService = _environmentDataServiceField.GetValue(environmentUi);
                return windowViewService != null && environmentDataService != null;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogDebug(
                    $"[环境运行时] 获取环境服务失败: {GetInvocationMessage(ex)}");
                return false;
            }
        }

        private bool TryApplySceneryStateDirect(
            EnvironmentType environmentType, bool targetState, bool targetSoundState)
        {
            if (!TryGetRuntimeServices(out _, out var windowViewService, out var environmentDataService) ||
                !EnsureDirectSceneryMetadata(windowViewService, environmentDataService) ||
                !TryGetRuntimeEnumValue(_windowViewEnumType, environmentType.ToString(), out var windowEnumValue))
            {
                return false;
            }

            bool hasWindowState = TryIsWindowActive(environmentType, out var isWindowActive);
            if (!hasWindowState || isWindowActive != targetState)
            {
                MethodInfo changeWindowMethod = targetState
                    ? _activateWindowMethod
                    : _deactivateWindowMethod;
                if (!TryInvoke(
                        changeWindowMethod,
                        windowViewService,
                        new[] { windowEnumValue },
                        "[环境运行时] 启动期窗景直连失败") ||
                    !TryInvoke(
                        _setViewActiveMethod,
                        environmentDataService,
                        new object[] { windowEnumValue, targetState },
                        "[环境运行时] 启动期窗景状态写入失败"))
                {
                    return false;
                }
            }

            if (TryGetRuntimeEnumValue(
                    _ambientSoundEnumType,
                    environmentType.ToString(),
                    out var ambientEnumValue) &&
                TryGetDirectSoundActive(environmentDataService, ambientEnumValue, out var isSoundActive) &&
                isSoundActive != targetSoundState)
            {
                return TryInvoke(
                    _setMuteMethod,
                    environmentDataService,
                    new object[] { ambientEnumValue, !targetSoundState },
                    "[环境运行时] 启动期环境音状态写入失败");
            }

            return true;
        }

        private bool EnsureDirectSceneryMetadata(object windowViewService, object environmentDataService)
        {
            Type windowViewServiceType = windowViewService.GetType();
            Type environmentDataServiceType = environmentDataService.GetType();
            if (_windowViewServiceType == windowViewServiceType &&
                _environmentDataServiceType == environmentDataServiceType)
            {
                return _activateWindowMethod != null &&
                    _deactivateWindowMethod != null &&
                    _setViewActiveMethod != null &&
                    _isMuteMethod != null &&
                    _setMuteMethod != null &&
                    _windowViewEnumType != null &&
                    _ambientSoundEnumType != null;
            }

            _windowViewServiceType = windowViewServiceType;
            _environmentDataServiceType = environmentDataServiceType;
            _activateWindowMethod = AccessTools.Method(windowViewServiceType, "ActivateWindow");
            _deactivateWindowMethod = AccessTools.Method(windowViewServiceType, "DeactivateWindow");
            _setViewActiveMethod = AccessTools.Method(environmentDataServiceType, "SetViewActive");
            _isMuteMethod = AccessTools.Method(environmentDataServiceType, "IsMute");
            _setMuteMethod = AccessTools.Method(environmentDataServiceType, "SetMute");
            _windowViewEnumType = GetSingleParameterType(_activateWindowMethod);
            _ambientSoundEnumType = GetSingleParameterType(_isMuteMethod);

            if (_setViewActiveMethod == null || _setMuteMethod == null)
            {
                return false;
            }

            ParameterInfo[] setViewActiveParameters = _setViewActiveMethod.GetParameters();
            ParameterInfo[] setMuteParameters = _setMuteMethod.GetParameters();
            return _activateWindowMethod != null &&
                _deactivateWindowMethod != null &&
                _isMuteMethod != null &&
                _windowViewEnumType != null &&
                _ambientSoundEnumType != null &&
                setViewActiveParameters.Length == 2 &&
                setViewActiveParameters[0].ParameterType == _windowViewEnumType &&
                setViewActiveParameters[1].ParameterType == typeof(bool) &&
                setMuteParameters.Length == 2 &&
                setMuteParameters[0].ParameterType == _ambientSoundEnumType &&
                setMuteParameters[1].ParameterType == typeof(bool);
        }

        private bool TryGetDirectSoundActive(
            object environmentDataService,
            object ambientEnumValue,
            out bool isActive)
        {
            isActive = false;
            try
            {
                isActive = !(bool)_isMuteMethod.Invoke(
                    environmentDataService,
                    new[] { ambientEnumValue });
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogDebug(
                    $"[环境运行时] 读取启动期环境音状态失败: {GetInvocationMessage(ex)}");
                return false;
            }
        }

        private bool TryIsControllerInTargetState(
            EnvironmentController controller,
            bool targetState,
            bool targetSoundState,
            out bool isInTargetState)
        {
            isInTargetState = false;
            if (!TryGetControllerCombinedState(
                    controller,
                    out var windowActive,
                    out var hasSound,
                    out var soundActive,
                    out var liveSoundActive))
            {
                return false;
            }

            isInTargetState = CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive,
                hasSound,
                soundActive,
                liveSoundActive,
                targetState,
                targetSoundState);
            return true;
        }

        private bool TrySetControllerCombinedState(
            EnvironmentController controller, bool targetState, bool targetSoundState)
        {
            if (!TryGetControllerCombinedState(
                    controller,
                    out var windowActive,
                    out var hasSound,
                    out var soundActive,
                    out var liveSoundActive))
            {
                return false;
            }

            bool needsWindowChange = CombinedEnvironmentStatePolicy.NeedsWindowChange(
                windowActive,
                targetState);
            bool needsSoundChange = CombinedEnvironmentStatePolicy.NeedsSoundChange(
                hasSound,
                soundActive,
                liveSoundActive,
                targetSoundState);
            if (!needsWindowChange && !needsSoundChange)
            {
                return true;
            }

            MethodInfo changeWindowMethod = needsWindowChange
                ? AccessTools.Method(controller.GetType(), "ChangeWindowView")
                : null;
            MethodInfo changeMuteMethod = needsSoundChange
                ? AccessTools.Method(
                    controller.GetType(),
                    targetSoundState ? "MuteDeactivate" : "MuteActivate")
                : null;
            if ((needsWindowChange && changeWindowMethod == null) ||
                (needsSoundChange && changeMuteMethod == null))
            {
                return false;
            }

            if (needsWindowChange)
            {
                Type changeType = GetSingleParameterType(changeWindowMethod);
                if (!TryGetRuntimeEnumValue(
                        changeType,
                        targetState ? "Activate" : "Deactivate",
                        out var changeValue) ||
                    !TryInvoke(
                        changeWindowMethod,
                        controller,
                        new[] { changeValue },
                        "[环境运行时] 窗景直连失败"))
                {
                    return false;
                }
            }

            return !needsSoundChange || TryInvoke(
                changeMuteMethod,
                controller,
                null,
                "[环境运行时] 环境音直连失败");
        }

        private bool TryGetControllerCombinedState(
            EnvironmentController controller,
            out bool windowActive,
            out bool hasSound,
            out bool soundActive,
            out bool liveSoundActive)
        {
            windowActive = false;
            hasSound = false;
            soundActive = false;
            liveSoundActive = false;
            if (controller == null)
            {
                return false;
            }

            try
            {
                FieldInfo dataServiceField = AccessTools.Field(
                    controller.GetType(),
                    "_environmentDataService");
                var dataService = dataServiceField?.GetValue(controller) as EnvironmentDataService;
                if (dataService == null)
                {
                    return false;
                }

                windowActive = dataService.IsWindowActive(controller.WindowViewType);

                FieldInfo ambientBehaviorField = AccessTools.Field(
                    controller.GetType(),
                    "_ambientSoundBehavior");
                if (ambientBehaviorField == null)
                {
                    return false;
                }
                object ambientBehavior = ambientBehaviorField.GetValue(controller);
                hasSound = ambientBehavior != null;
                if (!hasSound)
                {
                    return true;
                }

                soundActive = !dataService.IsMute(controller.AmbientSoundType);
                FieldInfo sliderField = AccessTools.Field(ambientBehavior.GetType(), "_volumeSlider");
                var slider = sliderField?.GetValue(ambientBehavior) as Slider;
                liveSoundActive = slider != null && slider.value > 0f;
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning(
                    $"[环境运行时] 读取组合状态失败: {GetInvocationMessage(ex)}");
                return false;
            }
        }

        private bool TryIsWindowViewActive(WindowViewType windowViewType, out bool isActive)
        {
            isActive = false;
            if (!TryGetWindowViewDictionary(out var windowViews) ||
                !windowViews.TryGetValue(windowViewType, out var data) ||
                data == null)
            {
                return false;
            }

            isActive = data.IsActive;
            return true;
        }

        private bool TryGetWindowViewDictionary(
            out Dictionary<WindowViewType, WindowViewData> windowViews)
        {
            windowViews = null;
            try
            {
                SaveDataManager saveData = SaveDataManager.Instance;
                if (saveData == null)
                {
                    return false;
                }

                var environmentData = saveData.EnviromentData;
                if (environmentData != null && environmentData.WindowViewDic != null)
                {
                    windowViews = environmentData.WindowViewDic;
                    return true;
                }

                PropertyInfo oldProperty = AccessTools.Property(saveData.GetType(), "WindowViewDic");
                windowViews = oldProperty?.GetValue(saveData, null) as
                    Dictionary<WindowViewType, WindowViewData>;
                if (windowViews != null && !_loggedSaveDataFallbackOnce)
                {
                    _loggedSaveDataFallbackOnce = true;
                    ChillEnvPlugin.Log?.LogWarning(
                        "[环境运行时] 使用旧版 SaveDataManager.WindowViewDic 读取窗景状态");
                }
                return windowViews != null;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogDebug(
                    $"[环境运行时] 读取窗景状态失败: {GetInvocationMessage(ex)}");
                return false;
            }
        }

        private static Type GetSingleParameterType(MethodInfo method)
        {
            if (method == null)
            {
                return null;
            }

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1 ? parameters[0].ParameterType : null;
        }

        private static bool TryGetRuntimeEnumValue(
            Type enumType,
            string valueName,
            out object enumValue)
        {
            enumValue = null;
            if (enumType == null || !enumType.IsEnum || !Enum.IsDefined(enumType, valueName))
            {
                return false;
            }

            enumValue = Enum.Parse(enumType, valueName);
            return true;
        }

        private static bool TryInvoke(
            MethodInfo method,
            object target,
            object[] arguments,
            string logPrefix)
        {
            if (method == null || target == null)
            {
                return false;
            }

            try
            {
                method.Invoke(target, arguments);
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"{logPrefix}: {GetInvocationMessage(ex)}");
                return false;
            }
        }

        private static string GetInvocationMessage(Exception ex)
        {
            var invocationException = ex as TargetInvocationException;
            return invocationException?.InnerException?.Message ?? ex.Message;
        }
    }
}
