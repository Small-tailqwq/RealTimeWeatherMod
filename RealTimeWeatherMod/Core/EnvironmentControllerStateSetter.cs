using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;
using HarmonyLib;
using UnityEngine.UI;

namespace ChillWithYou.EnvSync.Core
{
    internal static class EnvironmentControllerStateSetter
    {
        private static readonly FieldInfo EnvironmentDataServiceField =
            AccessTools.Field(typeof(EnvironmentController), "_environmentDataService");
        private static readonly FieldInfo AmbientSoundBehaviorField =
            AccessTools.Field(typeof(EnvironmentController), "_ambientSoundBehavior");
        private static readonly FieldInfo VolumeSliderField =
            AccessTools.Field(typeof(AmbientSoundBehavior), "_volumeSlider");

        internal static bool TryIsInTargetState(
            EnvironmentController controller,
            bool targetState,
            out bool isInTargetState)
        {
            isInTargetState = false;
            bool windowActive;
            bool hasSound;
            bool soundActive;
            bool liveSoundActive;
            if (!TryGetState(
                controller,
                out windowActive,
                out hasSound,
                out soundActive,
                out liveSoundActive))
            {
                return false;
            }

            isInTargetState = CombinedEnvironmentStatePolicy.IsInTargetState(
                windowActive,
                hasSound,
                soundActive,
                liveSoundActive,
                targetState);
            return true;
        }

        internal static bool TrySetCombinedState(EnvironmentController controller, bool targetState)
        {
            bool windowActive;
            bool hasSound;
            bool soundActive;
            bool liveSoundActive;
            if (!TryGetState(
                controller,
                out windowActive,
                out hasSound,
                out soundActive,
                out liveSoundActive))
            {
                return false;
            }

            bool needsWindowChange =
                CombinedEnvironmentStatePolicy.NeedsWindowChange(windowActive, targetState);
            bool needsSoundChange =
                CombinedEnvironmentStatePolicy.NeedsSoundChange(
                    hasSound,
                    soundActive,
                    liveSoundActive,
                    targetState);
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
                    targetState ? "MuteDeactivate" : "MuteActivate")
                : null;

            if ((needsWindowChange && changeWindowMethod == null) ||
                (needsSoundChange && changeMuteMethod == null))
            {
                return false;
            }

            object changeType = null;

            try
            {
                if (needsWindowChange)
                {
                    var parameters = changeWindowMethod.GetParameters();
                    if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
                    {
                        return false;
                    }

                    changeType = Enum.Parse(
                        parameters[0].ParameterType,
                        targetState ? "Activate" : "Deactivate");
                    changeWindowMethod.Invoke(controller, new[] { changeType });
                }
                if (needsSoundChange)
                {
                    if (changeMuteMethod.GetParameters().Length != 0)
                    {
                        return false;
                    }
                    changeMuteMethod.Invoke(controller, null);
                }
                return true;
            }
            catch (ArgumentException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TrySetCombinedState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (TargetException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TrySetCombinedState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (MethodAccessException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TrySetCombinedState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (TargetParameterCountException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TrySetCombinedState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (TargetInvocationException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TrySetCombinedState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static bool TryGetState(
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
            if (controller == null ||
                EnvironmentDataServiceField == null ||
                AmbientSoundBehaviorField == null)
            {
                return false;
            }

            try
            {
                var dataService = EnvironmentDataServiceField.GetValue(controller) as EnvironmentDataService;
                if (dataService == null)
                {
                    return false;
                }

                windowActive = dataService.IsWindowActive(controller.WindowViewType);

                var ambientBehavior = AmbientSoundBehaviorField.GetValue(controller);
                hasSound = ambientBehavior != null;
                if (!hasSound)
                {
                    return true;
                }

                if (VolumeSliderField == null)
                {
                    ChillEnvPlugin.Log?.LogWarning("[StateSetter] TryGetState: _volumeSlider 字段未找到，无法读取实时音量状态");
                    return false;
                }

                soundActive = !dataService.IsMute(controller.AmbientSoundType);
                var volumeSlider = VolumeSliderField.GetValue(ambientBehavior) as Slider;
                liveSoundActive = volumeSlider != null && volumeSlider.value > 0f;
                return true;
            }
            catch (NullReferenceException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TryGetState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (KeyNotFoundException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TryGetState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (ArgumentException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TryGetState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (TargetException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TryGetState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            catch (FieldAccessException ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[StateSetter] TryGetState 异常: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }
}
