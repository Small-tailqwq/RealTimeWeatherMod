using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Bulbul;

namespace ChillWithYou.EnvSync
{
  [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", "5.1.3")]
  public class ChillEnvPlugin : BaseUnityPlugin
  {
    internal static ChillEnvPlugin Instance;
    internal static ManualLogSource Log;
    internal static UnlockItemService UnlockItemServiceInstance;

    internal static object WindowViewServiceInstance;
    internal static MethodInfo ChangeWeatherMethod;
    internal static string UIWeatherString = "";
    internal static bool Initialized;

    // --- 配置项 ---
    internal static ConfigEntry<int> Cfg_WeatherRefreshMinutes;
    internal static ConfigEntry<string> Cfg_SunriseTime;
    internal static ConfigEntry<string> Cfg_SunsetTime;
    internal static ConfigEntry<string> Cfg_SeniverseKey;
    internal static ConfigEntry<string> Cfg_Location;
    internal static ConfigEntry<bool> Cfg_EnableWeatherSync;
    internal static ConfigEntry<bool> Cfg_UnlockEnvironments;
    internal static ConfigEntry<bool> Cfg_UnlockDecorations;
    internal static ConfigEntry<bool> Cfg_UnlockPurchasableItems;

    // UI 配置
    internal static ConfigEntry<bool> Cfg_ShowWeatherOnUI;
    // 【5.1.1 新增】是否启用详细时间段显示 (凌晨/清晨/上午...)
    internal static ConfigEntry<bool> Cfg_DetailedTimeSegments;

    internal static ConfigEntry<bool> Cfg_EnableEasterEggs;

    // 调试配置
    internal static ConfigEntry<bool> Cfg_DebugMode;
    internal static ConfigEntry<int> Cfg_DebugCode;
    internal static ConfigEntry<int> Cfg_DebugTemp;
    internal static ConfigEntry<string> Cfg_DebugText;

    // [Hidden] 上次同步日出日落的日期
    internal static ConfigEntry<string> Cfg_LastSunSyncDate;

    private static GameObject _runnerGO;

    private void Awake()
    {
      Instance = this;
      Log = Logger;

      Log.LogWarning("【5.1.3】启动 - 解锁调试版");

      try
      {
        var harmony = new Harmony("ChillWithYou.EnvSync");
        harmony.PatchAll();
        Patches.UnlockConditionGodMode.ApplyPatches(harmony);
      }
      catch (Exception ex)
      {
        Log.LogError($"Harmony 失败: {ex}");
      }

      InitConfig();

      try
      {
        _runnerGO = new GameObject("ChillEnvSyncRunner");
        _runnerGO.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(_runnerGO);
        _runnerGO.SetActive(true);

        // 挂载组件
        _runnerGO.AddComponent<Core.AutoEnvRunner>();
        _runnerGO.AddComponent<Core.SceneryAutomationSystem>();
        _runnerGO.AddComponent<Patches.ModSettingsIntegration>();
      }
      catch (Exception ex)
      {
        Log.LogError($"Runner 创建失败: {ex}");
      }
    }

    private void InitConfig()
    {
      Cfg_WeatherRefreshMinutes = Config.Bind("WeatherSync", "RefreshMinutes", 30, "天气API刷新间隔(分钟)");
      Cfg_SunriseTime = Config.Bind("TimeConfig", "Sunrise", "06:30", "日出时间");
      Cfg_SunsetTime = Config.Bind("TimeConfig", "Sunset", "18:30", "日落时间");

      Cfg_EnableWeatherSync = Config.Bind("WeatherAPI", "EnableWeatherSync", false, "是否启用天气API同步");
      Cfg_SeniverseKey = Config.Bind("WeatherAPI", "SeniverseKey", "", "心知天气 API Key");
      Cfg_Location = Config.Bind("WeatherAPI", "Location", "beijing", "城市名称");

      Cfg_UnlockEnvironments = Config.Bind("Unlock", "UnlockAllEnvironments", true, "自动解锁环境");
      Cfg_UnlockDecorations = Config.Bind("Unlock", "UnlockAllDecorations", true, "自动解锁装饰道具");
      Cfg_UnlockPurchasableItems = Config.Bind("Unlock", "UnlockPurchasableItems", false, "解锁游戏币购买内容");

      Cfg_ShowWeatherOnUI = Config.Bind("UI", "ShowWeatherOnDate", true, "日期栏显示天气");
      // 【新增配置】
      Cfg_DetailedTimeSegments = Config.Bind("UI", "DetailedTimeSegments", true, "开启12小时制时，显示详细时段(凌晨/清晨/上午等)");

      Cfg_EnableEasterEggs = Config.Bind("Automation", "EnableSeasonalEasterEggs", true, "启用季节性彩蛋与环境音效自动托管");

      Cfg_DebugMode = Config.Bind("Debug", "EnableDebugMode", false, "调试模式");
      Cfg_DebugCode = Config.Bind("Debug", "SimulatedCode", 1, "模拟天气代码");
      Cfg_DebugTemp = Config.Bind("Debug", "SimulatedTemp", 25, "模拟温度");
      Cfg_DebugText = Config.Bind("Debug", "SimulatedText", "DebugWeather", "模拟描述");

      // [Hidden] 上次同步日出日落的日期
      Cfg_LastSunSyncDate = Config.Bind("Internal", "LastSunSyncDate", "", "上次同步日期");
    }

    internal static void TryInitializeOnce(UnlockItemService svc)
    {
      if (Initialized || svc == null) return;

      if (Cfg_UnlockEnvironments.Value) ForceUnlockAllEnvironments(svc);
      if (Cfg_UnlockDecorations.Value) ForceUnlockAllDecorations(svc);

      Initialized = true;
      Log?.LogInfo("初始化完成");

      // 如果启用了调试模式，延迟验证解锁状态
      if (Cfg_DebugMode.Value && Instance != null)
      {
        Instance.StartCoroutine(VerifyUnlockAfterDelay(svc, 3f));
      }
    }

    // 延迟验证解锁状态（检查是否被重新锁定）
    private static System.Collections.IEnumerator VerifyUnlockAfterDelay(UnlockItemService svc, float delay)
    {
      yield return new WaitForSeconds(delay);
      Log?.LogInfo($"[调试] {delay}秒后验证解锁状态...");

      int lockedEnvCount = 0;
      int lockedDecoCount = 0;

      try
      {
        var envProp = svc.GetType().GetProperty("Environment");
        var unlockEnvObj = envProp.GetValue(svc);
        var dictField = unlockEnvObj.GetType().GetField("_environmentDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var dict = dictField.GetValue(unlockEnvObj) as System.Collections.IDictionary;

        foreach (System.Collections.DictionaryEntry entry in dict)
        {
          var data = entry.Value;
          var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
          var reactive = lockField.GetValue(data);
          var propValue = reactive.GetType().GetProperty("Value");
          bool isLocked = (bool)propValue.GetValue(reactive, null);
          if (isLocked)
          {
            lockedEnvCount++;
            Log?.LogWarning($"[调试] ⚠️ 环境 {entry.Key} 被重新锁定!");
          }
        }
      }
      catch (Exception ex) { Log?.LogError($"[调试] 验证环境失败: {ex.Message}"); }

      try
      {
        var decoProp = svc.GetType().GetProperty("Decoration");
        var unlockDecoObj = decoProp?.GetValue(svc);
        if (unlockDecoObj != null)
        {
          var dictField = unlockDecoObj.GetType().GetField("_decorationDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
          var dict = dictField?.GetValue(unlockDecoObj) as System.Collections.IDictionary;

          if (dict != null)
          {
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
              var data = entry.Value;
              var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
              var reactive = lockField?.GetValue(data);
              if (reactive != null)
              {
                var propValue = reactive.GetType().GetProperty("Value");
                bool isLocked = (bool)propValue.GetValue(reactive, null);
                if (isLocked)
                {
                  lockedDecoCount++;
                  Log?.LogWarning($"[调试] ⚠️ 装饰品 {entry.Key} 被重新锁定!");
                }
              }
            }
          }
        }
      }
      catch (Exception ex) { Log?.LogError($"[调试] 验证装饰品失败: {ex.Message}"); }

      if (lockedEnvCount == 0 && lockedDecoCount == 0)
      {
        Log?.LogInfo($"[调试] ✅ 验证通过: 所有解锁状态保持正常");
      }
      else
      {
        Log?.LogError($"[调试] ❌ 发现问题: {lockedEnvCount} 个环境和 {lockedDecoCount} 个装饰品被重新锁定");
        Log?.LogError($"[调试] 可能原因: 游戏在初始化后重新加载了存档数据");
      }
    }

    internal static void CallServiceChangeWeather(EnvironmentType envType)
    {
        // ---------------------------------------------------------
        // 第一阶段：寻找 EnvironmentUI 实例
        // ---------------------------------------------------------
        MonoBehaviour targetUI = null;
        
        // 我们不需要缓存 targetUI，因为 UI 可能会被销毁或重载，每次动态找最稳
        Type uiType = AccessTools.TypeByName("Bulbul.EnvironmentUI");
        if (uiType != null)
        {
            // 暴力查找所有 EnvironmentUI (包括隐藏的)
            var allUIs = UnityEngine.Resources.FindObjectsOfTypeAll(uiType);
            if (allUIs != null && allUIs.Length > 0)
            {
                // 优先找场景里的
                foreach (var obj in allUIs)
                {
                    var mono = obj as MonoBehaviour;
                    // 过滤掉 Asset 资源，只找 Scene 里的对象
                    if (mono != null && mono.gameObject.scene.rootCount != 0)
                    {
                        targetUI = mono;
                        break;
                    }
                }
            }
        }

        if (targetUI == null)
        {
            // 如果连 UI 都找不到，说明可能在主菜单或者加载中，直接跳过
            return;
        }

        // ---------------------------------------------------------
        // 第二阶段：调用 EnvironmentUI.ChangeTime
        // ---------------------------------------------------------
        try
        {
            // 目标方法：private void ChangeTime(EnvironmentType environmentType)
            // 这个方法是私有的，而且它会自动处理 SaveData 的更新，完美解决死循环
            var changeTimeMethod = AccessTools.Method(targetUI.GetType(), "ChangeTime");

            if (changeTimeMethod != null)
            {
                var parameters = changeTimeMethod.GetParameters();
                if (parameters.Length > 0)
                {
                    // 获取目标方法的枚举类型 (Bulbul.EnvironmentType)
                    Type targetEnumType = parameters[0].ParameterType;
                    
                    // 将我们的 envType 转为目标枚举
                    object enumValue = Enum.Parse(targetEnumType, envType.ToString());

                    // 执行调用
                    changeTimeMethod.Invoke(targetUI, new object[] { enumValue });
                    
                    // 成功日志
                    Log?.LogInfo($"[Service] 🌧️ 天气已切换并同步状态: {envType}");
                }
            }
            else
            {
                Log?.LogError("[Service] ❌ 找不到 ChangeTime 方法，游戏版本可能不匹配");
            }
        }
        catch (Exception ex)
        {
            Log?.LogError($"[Service] ❌ 调用 ChangeTime 失败: {ex.Message}");
        }
    }

    internal static void SimulateClickMainIcon(EnvironmentController ctrl)
    {
      if (ctrl == null) return;
      try
      {
        Log?.LogInfo($"[SimulateClick] 准备点击: {ctrl.name} (Type: {ctrl.GetType().Name})");
        MethodInfo clickMethod = ctrl.GetType().GetMethod("OnClickButtonMainIcon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (clickMethod != null)
        {
          Patches.UserInteractionPatch.IsSimulatingClick = true;
          clickMethod.Invoke(ctrl, null);
          Patches.UserInteractionPatch.IsSimulatingClick = false;
          Log?.LogInfo($"[SimulateClick] 点击调用完成: {ctrl.name}");
        }
        else
        {
          Log?.LogError($"[SimulateClick] ❌ 未找到 OnClickButtonMainIcon 方法: {ctrl.name}");
        }
      }
      catch (Exception ex) { Log?.LogError($"模拟点击失败: {ex.Message}"); }
    }

    private static void ForceUnlockAllEnvironments(UnlockItemService svc)
    {
      try
      {
        var envProp = svc.GetType().GetProperty("Environment");
        var unlockEnvObj = envProp.GetValue(svc);
        var dictField = unlockEnvObj.GetType().GetField("_environmentDic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var dict = dictField.GetValue(unlockEnvObj) as System.Collections.IDictionary;
        int count = 0;
        int verifyCount = 0;
        foreach (System.Collections.DictionaryEntry entry in dict)
        {
          var data = entry.Value;
          var lockField = data.GetType().GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
          var reactive = lockField.GetValue(data);
          var propValue = reactive.GetType().GetProperty("Value");

          // 解锁前记录状态
          bool beforeUnlock = (bool)propValue.GetValue(reactive, null);

          // 执行解锁
          propValue.SetValue(reactive, false, null);
          count++;

          // 验证解锁是否成功
          bool afterUnlock = (bool)propValue.GetValue(reactive, null);
          if (!afterUnlock) verifyCount++;

          if (Cfg_DebugMode.Value)
          {
            Log?.LogInfo($"[环境解锁] {entry.Key}: {beforeUnlock} -> {afterUnlock}");
          }
        }
        Log?.LogInfo($"✅ 已解锁 {count} 个环境 (验证成功: {verifyCount})");
      }
      catch (Exception ex)
      {
        Log?.LogError($"环境解锁失败: {ex}");
      }
    }

    //private static void ForceUnlockAllDecorations(UnlockItemService svc)
    /// <summary>
    /// 【通用解锁核弹】
    /// 暴力扫描 UnlockItemService 下的所有属性，只要发现内部含有 IDictionary 且元素含有 _isLocked 字段，一律解锁！
    /// </summary>
    /// <summary>
    /// 【通用解锁核弹 v2 - 钻地弹版】
    /// 升级逻辑：不仅扫描 Property，还暴力扫描所有 Private Fields (特别是 _conditionService)。
    /// 只要发现内部含有 IDictionary 且元素含有 _isLocked 字段，一律解锁！
    /// </summary>
    private static void ForceUnlockAllDecorations(UnlockItemService svc)
    {
        if (svc == null) return;

        Log?.LogInfo("☢️ 启动通用解锁核弹 v2 (钻地模式)...");
        int totalUnlocked = 0;

        try
        {
            // 1. 获取 Service 下的所有字段 (包括私有的 _conditionService, _environment, _decoration 等)
            // 使用 Harmony 的 AccessTools.GetDeclaredFields 可以无视访问权限拿到所有字段
            var serviceFields = AccessTools.GetDeclaredFields(svc.GetType());

            foreach (var field in serviceFields)
            {
                // 跳过一些明显不是目标的数据加载器，防止浪费时间或报错
                if (field.FieldType.Name.Contains("MasterData") || field.FieldType.Name.Contains("Loader")) 
                    continue;

                // 获取字段的值 (例如 UnlockConditionService 实例)
                object componentObj = field.GetValue(svc);
                if (componentObj == null) continue;

                // Log?.LogInfo($"👉 正在扫描组件: {field.Name} ({componentObj.GetType().Name})");

                // 2. 深入扫描这个组件内部的所有字典
                var subFields = AccessTools.GetDeclaredFields(componentObj.GetType());
                
                foreach (var subField in subFields)
                {
                    // 必须是字典类型
                    if (!typeof(System.Collections.IDictionary).IsAssignableFrom(subField.FieldType)) continue;

                    var dict = subField.GetValue(componentObj) as System.Collections.IDictionary;
                    if (dict == null || dict.Count == 0) continue;

                    int groupCount = 0;
                    
                    // 3. 遍历字典内容
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        var dataItem = entry.Value;
                        if (dataItem == null) continue;

                        // 4. 暴力查找 _isLocked 字段
                        var lockField = AccessTools.Field(dataItem.GetType(), "_isLocked");
                        if (lockField == null) continue;

                        var reactiveBool = lockField.GetValue(dataItem);
                        if (reactiveBool == null) continue;

                        // R3/UniRx 的 ReactiveProperty.Value
                        var valueProp = reactiveBool.GetType().GetProperty("Value");
                        if (valueProp == null) continue;

                        // 5. 执行解锁！
                        bool isLocked = (bool)valueProp.GetValue(reactiveBool, null);
                        if (isLocked)
                        {
                            valueProp.SetValue(reactiveBool, false, null);
                            groupCount++;
                            totalUnlocked++;
                            if (Cfg_DebugMode.Value)
                            {
                                // 打印一下 Key，看看解锁了啥 (颜色变种通常 key 会很长或者是数字)
                                Log?.LogInfo($"   🔓 解锁: {entry.Key} (在 {field.Name}.{subField.Name})");
                            }
                        }
                    }

                    if (groupCount > 0)
                    {
                        Log?.LogInfo($"✅ 在 {field.Name} -> {subField.Name} 中解锁了 {groupCount} 个项目");
                    }
                }
            }
            Log?.LogInfo($"🎉 核弹 v2 投放完毕，本次共解锁 {totalUnlocked} 个项目！");
        }
        catch (Exception ex)
        {
            Log?.LogError($"❌ 通用解锁 v2 失败: {ex}");
        }
    }
  }
}