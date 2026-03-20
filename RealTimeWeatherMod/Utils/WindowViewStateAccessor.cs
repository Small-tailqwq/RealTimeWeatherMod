using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;

namespace ChillWithYou.EnvSync.Utils
{
    internal static class WindowViewStateAccessor
    {
        private static bool _loggedFallbackOnce;

        internal static bool TryIsWindowViewActive(WindowViewType viewType, out bool isActive)
        {
            isActive = false;
            if (!TryGetWindowViewDic(out var dict)) return false;
            if (!dict.TryGetValue(viewType, out var data) || data == null) return false;

            isActive = data.IsActive;
            return true;
        }

        private static bool TryGetWindowViewDic(out Dictionary<WindowViewType, WindowViewData> dict)
        {
            dict = null;

            try
            {
                var saveData = SaveDataManager.Instance;
                if (saveData == null) return false;

                // 新版本路径：SaveDataManager.Instance.EnviromentData.WindowViewDic
                var envData = saveData.EnviromentData;
                if (envData != null && envData.WindowViewDic != null)
                {
                    dict = envData.WindowViewDic;
                    return true;
                }

                // 旧版本兜底：SaveDataManager.Instance.WindowViewDic
                var oldProp = saveData.GetType().GetProperty("WindowViewDic", BindingFlags.Instance | BindingFlags.Public);
                if (oldProp != null)
                {
                    dict = oldProp.GetValue(saveData) as Dictionary<WindowViewType, WindowViewData>;
                    if (dict != null)
                    {
                        if (!_loggedFallbackOnce)
                        {
                            _loggedFallbackOnce = true;
                            ChillEnvPlugin.Log?.LogWarning("[兼容] 使用旧版 SaveDataManager.WindowViewDic 读取窗景状态。");
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogDebug($"[兼容] 读取窗景状态失败: {ex.Message}");
            }

            return false;
        }
    }
}