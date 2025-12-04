using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;

namespace ChillWithYou.EnvSync.Patches
{
    /// <summary>
    /// MOD 设置界面集成 (使用新 API)
    /// 负责将核心配置项添加到游戏设置界面的 MOD 标签页中
    /// 
    /// 更新日志:
    /// - v2.0: 迁移到新 API (AddToggle 不再需要 parent 参数)
    /// - v1.0: 初始版本
    /// </summary>
    public class ModSettingsIntegration : MonoBehaviour
    {
        private static bool _settingsRegistered = false;

        private void Start()
        {
            StartCoroutine(RegisterSettingsWhenReady());
        }

        private IEnumerator RegisterSettingsWhenReady()
        {
            yield return null;

            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (TryRegisterSettings())
                {
                    ChillEnvPlugin.Log?.LogInfo("✅ MOD 设置已成功注册到游戏界面");
                    yield break;
                }

                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            ChillEnvPlugin.Log?.LogWarning("⚠️ ModSettingsManager 未找到,设置界面功能不可用 (可能是 iGPU Savior 未安装)");
        }

        private bool TryRegisterSettings()
        {
            if (_settingsRegistered) return true;

            try
            {
                // 获取 ModSettingsManager
                Type managerType = Type.GetType("ModShared.ModSettingsManager, iGPU Savior");
                if (managerType == null)
                {
                    return false;
                }

                var instanceProp = managerType.GetProperty("Instance", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (instanceProp == null)
                {
                    ChillEnvPlugin.Log?.LogError("❌ ModSettingsManager.Instance 属性不存在");
                    return false;
                }

                object managerInstance = instanceProp.GetValue(null);
                if (managerInstance == null)
                {
                    return false;
                }

                var isInitializedProp = managerType.GetProperty("IsInitialized");
                if (isInitializedProp == null)
                {
                    ChillEnvPlugin.Log?.LogError("❌ ModSettingsManager.IsInitialized 属性不存在");
                    return false;
                }

                bool isInitialized = (bool)isInitializedProp.GetValue(managerInstance);
                if (!isInitialized)
                {
                    return false;
                }

                // ========== 注册设置项 (使用新 API) ==========

                bool allSuccess = true;

                // 1. 启用天气API同步
                if (!AddToggleSafe(managerInstance, managerType,
                    "启用天气API同步",
                    ChillEnvPlugin.Cfg_EnableWeatherSync.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableWeatherSync.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 天气API同步: {value}");
                    }))
                {
                    allSuccess = false;
                }

                // 2. 日期栏显示天气信息
                if (!AddToggleSafe(managerInstance, managerType,
                    "日期栏显示天气信息",
                    ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_ShowWeatherOnUI.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 显示天气信息: {value}");
                    }))
                {
                    allSuccess = false;
                }

                // 3. 显示详细时段
                if (!AddToggleSafe(managerInstance, managerType,
                    "显示详细时段(凌晨/清晨/上午等)",
                    ChillEnvPlugin.Cfg_DetailedTimeSegments.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_DetailedTimeSegments.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 详细时段: {value}");
                    }))
                {
                    allSuccess = false;
                }

                // 4. 启用季节性彩蛋
                if (!AddToggleSafe(managerInstance, managerType,
                    "启用季节性彩蛋与环境音效",
                    ChillEnvPlugin.Cfg_EnableEasterEggs.Value,
                    (value) =>
                    {
                        ChillEnvPlugin.Cfg_EnableEasterEggs.Value = value;
                        ChillEnvPlugin.Instance.Config.Save();
                        ChillEnvPlugin.Log?.LogInfo($"[设置] 季节性彩蛋: {value}");
                    }))
                {
                    allSuccess = false;
                }

                if (allSuccess)
                {
                    ChillEnvPlugin.Log?.LogInfo("✅ 所有设置项已成功添加");
                    
                    // 修正布局偏移问题
                    FixContentLayout(managerInstance, managerType);
                }

                _settingsRegistered = true;
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"❌ 注册设置失败: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 安全的添加 Toggle 设置项 (使用新 API - 不需要传入 parent)
        /// </summary>
        private bool AddToggleSafe(object managerInstance, Type managerType,
            string label, bool defaultValue, Action<bool> callback)
        {
            try
            {
                // 获取新版 AddToggle 方法 (3个参数: label, defaultValue, callback)
                var addToggleMethod = managerType.GetMethod("AddToggle", new Type[] {
                    typeof(string),
                    typeof(bool),
                    typeof(Action<bool>)
                });

                if (addToggleMethod == null)
                {
                    ChillEnvPlugin.Log?.LogError("❌ ModSettingsManager.AddToggle(string, bool, Action<bool>) 方法不存在");
                    return false;
                }

                // 包装回调以捕获异常
                Action<bool> safeCallback = (value) =>
                {
                    try
                    {
                        callback?.Invoke(value);
                    }
                    catch (Exception ex)
                    {
                        ChillEnvPlugin.Log?.LogError($"❌ 设置 '{label}' 的回调异常: {ex.Message}");
                    }
                };

                object result = addToggleMethod.Invoke(managerInstance, new object[] {
                    label,
                    defaultValue,
                    safeCallback
                });

                // 调试: 检查返回的GameObject
                GameObject toggleObj = result as GameObject;
                if (toggleObj != null)
                {
                    ChillEnvPlugin.Log?.LogInfo($"✅ 已添加设置: '{label}' → GameObject: {toggleObj.name}, Active: {toggleObj.activeSelf}");
                }
                else
                {
                    ChillEnvPlugin.Log?.LogInfo($"✅ 已添加设置: '{label}'");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"❌ 添加设置 '{label}' 失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 修正内容布局 - 解决 -145 偏移问题
        /// </summary>
        private void FixContentLayout(object managerInstance, Type managerType)
        {
            try
            {
                // 获取 ModContentParent
                var contentParentProp = managerType.GetProperty("ModContentParent");
                if (contentParentProp == null)
                {
                    ChillEnvPlugin.Log?.LogWarning("⚠️ 无法获取 ModContentParent 属性,跳过布局修正");
                    return;
                }

                GameObject contentParent = contentParentProp.GetValue(managerInstance) as GameObject;
                if (contentParent == null)
                {
                    ChillEnvPlugin.Log?.LogWarning("⚠️ ModContentParent 为 null,跳过布局修正");
                    return;
                }

                ConfigureContentLayout(contentParent);
                ChillEnvPlugin.Log?.LogInfo("✅ 内容布局已修正");
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"⚠️ 布局修正失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 配置内容布局 - 修正偏移和对齐问题
        /// </summary>
        static void ConfigureContentLayout(GameObject content)
        {
            // 1. 强制重置 Content 的 RectTransform
            var rect = content.GetComponent<RectTransform>();
            if (rect != null)
            {
                // 关键：Anchor Min=(0,1), Max=(1,1) -> 横向拉伸，顶部对齐
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1f); // 轴心点在顶部中心
                rect.anchoredPosition = Vector2.zero; // 归位
                rect.sizeDelta = new Vector2(0, 0); // 宽度自适应（由父级控制），高度由Fitter控制
                rect.localScale = Vector3.one;
            }

            // 2. 配置垂直布局组
            var vGroup = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
            vGroup.spacing = 16f;
            // Padding: Left=60 (给标题留空间), Right=40
            vGroup.padding = new RectOffset(60, 40, 20, 20);
            vGroup.childAlignment = TextAnchor.UpperLeft; // 强制左上对齐
            vGroup.childControlHeight = false;
            vGroup.childControlWidth = true;
            vGroup.childForceExpandHeight = false;
            vGroup.childForceExpandWidth = true;   // 强制子物体撑满宽度

            // 3. 配置大小适配器
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }
}
