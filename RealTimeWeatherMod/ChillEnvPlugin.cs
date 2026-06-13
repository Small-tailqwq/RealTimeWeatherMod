// Test OCR review pipeline for C# project
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
  [BepInPlugin("chillwithyou.envsync", "Chill Env Sync", PluginVersion)]
  public class ChillEnvPlugin : BaseUnityPlugin
  {
    internal const string PluginVersion = "5.2.2";
    internal const string BuildId = "controller-readiness-20260606";

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
    internal static ConfigEntry<bool> Cfg_EnableTimeSync;
    internal static ConfigEntry<bool> Cfg_EnableWeatherSync;
    internal static ConfigEntry<bool> Cfg_UnlockEnvironments;
    internal static ConfigEntry<bool> Cfg_UnlockDecorations;
    internal static ConfigEntry<bool> Cfg_UnlockPurchasableItems;

    // UI 配置
    internal static ConfigEntry<bool> Cfg_ShowWeatherOnUI;
    // 【5.1.1 新增】是否启用详细时间段显示 (凌晨/清晨/上午...)
    internal static ConfigEntry<bool> Cfg_DetailedTimeSegments;

    internal static ConfigEntry<bool> Cfg_EnableEasterEggs;
    internal static ConfigEntry<bool> Cfg_EnableAmbientSounds;

    // 调试配置
    internal static ConfigEntry<bool> Cfg_DebugMode;
    internal static ConfigEntry<int> Cfg_DebugCode;
    internal static ConfigEntry<int> Cfg_DebugTemp;
    internal static ConfigEntry<string> Cfg_DebugText;

    // [Hidden] 上次同步日出日落的日期
    internal static ConfigEntry<string> Cfg_LastSunSyncDate;
    internal static ConfigEntry<string> Cfg_AutoManagedEnvironments;

    private static GameObject _runnerGO;

    private void Awake()
    {
      Instance = this;
      Log = Logger;

      Log.LogInfo($"【{PluginVersion}】启动 - Build={BuildId}");

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
      Cfg_EnableTimeSync = Config.Bind("TimeSync", "EnableTimeSync", true, "是否启用时间同步");

      Cfg_EnableWeatherSync = Config.Bind("WeatherAPI", "EnableWeatherSync", false, "是否启用天气API同步");
      Cfg_SeniverseKey = Config.Bind("WeatherAPI", "SeniverseKey", "", "心知天气 API Key");
      Cfg_Location = Config.Bind("WeatherAPI", "Location", "beijing", "城市名称");

      Cfg_UnlockEnvironments = Config.Bind("Unlock", "UnlockAllEnvironments", true, "自动解锁环境");
      Cfg_UnlockDecorations = Config.Bind("Unlock", "UnlockAllDecorations", true, "自动解锁装饰道具");
      Cfg_UnlockPurchasableItems = Config.Bind("Unlock", "UnlockPurchasableItems", false, "解锁游戏币购买内容");

      Cfg_ShowWeatherOnUI = Config.Bind("UI", "ShowWeatherOnDate", true, "日期栏显示天气");
      // 【新增配置】
      Cfg_DetailedTimeSegments = Config.Bind("UI", "DetailedTimeSegments", true, "开启12小时制时，显示详细时段(凌晨/清晨/上午等)");

      Cfg_EnableEasterEggs = Config.Bind("Automation", "EnableSeasonalEasterEggs", true, "启用季节性景色彩蛋自动托管");
      Cfg_EnableAmbientSounds = Config.Bind("Automation", "EnableAmbientSounds", false, "启用环境音效自动托管");

      Cfg_DebugMode = Config.Bind("Debug", "EnableDebugMode", false, "调试模式");
      Cfg_DebugCode = Config.Bind("Debug", "SimulatedCode", 1, "模拟天气代码");
      Cfg_DebugTemp = Config.Bind("Debug", "SimulatedTemp", 25, "模拟温度");
      Cfg_DebugText = Config.Bind("Debug", "SimulatedText", "DebugWeather", "模拟描述");

      // [Hidden] 上次同步日出日落的日期
      Cfg_LastSunSyncDate = Config.Bind("Internal", "LastSunSyncDate", "", "上次同步日期");
      Cfg_AutoManagedEnvironments = Config.Bind(
        "Internal",
        "AutoManagedEnvironments",
        "",
        "由自动托管开启的环境，用于跨会话安全清理");
    }

    internal static void TryInitializeOnce(UnlockItemService svc)
    {
      if (Initialized || svc == null) return;

      if (Cfg_UnlockEnvironments.Value) ForceUnlockAllEnvironments(svc);
      if (Cfg_UnlockDecorations.Value) ForceUnlockAllDecorations(svc);

      Initialized = true;
      Log?.LogInfo("初始化完成");

      // 立即触发首次天气同步（等游戏就绪后自动执行）
      Core.AutoEnvRunner.TriggerImmediateSync();

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
        
        Type uiType = AccessTools.TypeByName("Bulbul.EnvironmentUI");
        if (uiType != null)
        {
            var allUIs = UnityEngine.Resources.FindObjectsOfTypeAll(uiType);
            if (allUIs != null && allUIs.Length > 0)
            {
                foreach (var obj in allUIs)
                {
                    var mono = obj as MonoBehaviour;
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
            return;
        }

        // ---------------------------------------------------------
        // 第二阶段：调用 EnvironmentUI.OnClickButtonChangeTime
        // 这个 public 方法会：检查锁定状态、关闭 auto（如开启）、跳过已激活的目标
        // 最后调用内部 ChangeTime 执行实际切换
        // ---------------------------------------------------------
        try
        {
            var method = AccessTools.Method(targetUI.GetType(), "OnClickButtonChangeTime");

            if (method != null)
            {
                var parameters = method.GetParameters();
                if (parameters.Length > 0)
                {
                    Type targetEnumType = parameters[0].ParameterType;
                    object enumValue = Enum.Parse(targetEnumType, envType.ToString());
                    method.Invoke(targetUI, new object[] { enumValue });
                    
                    Log?.LogInfo($"[Service] 环境已切换: {envType}");
                }
            }
            else
            {
                Log?.LogError("[Service] ❌ 找不到 OnClickButtonChangeTime 方法，游戏版本可能不匹配");
            }
        }
        catch (Exception ex)
        {
            Log?.LogError($"[Service] ❌ 调用 OnClickButtonChangeTime 失败: {ex.Message}");
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
          try
          {
            clickMethod.Invoke(ctrl, null);
            Log?.LogInfo($"[SimulateClick] 点击调用完成: {ctrl.name}");
          }
          finally
          {
            Patches.UserInteractionPatch.IsSimulatingClick = false;
          }
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

        Log?.LogInfo("☢️ 启动通用解锁核弹 v3 (定向模式)...");

        try
        {
            int totalUnlocked = Services.DecorationUnlockScanner.Unlock(
                svc,
                message => Log?.LogInfo(message),
                message => Log?.LogError(message),
                Cfg_DebugMode.Value);

            Log?.LogInfo($"🎉 核弹 v3 投放完毕，本次共解锁 {totalUnlocked} 个项目！");
        }
        catch (Exception ex)
        {
            Log?.LogError($"❌ 通用解锁 v3 失败: {ex}");
        }
    }

    internal static bool IsInCutscene()
    {
      try
      {
        RoomGameManager roomGM = GameObject.FindObjectOfType<RoomGameManager>();
        if (roomGM == null)
        {
          var all = Resources.FindObjectsOfTypeAll<RoomGameManager>();
          if (all == null || all.Length == 0) return false;
          roomGM = all[0];
        }

        if (roomGM == null) return false;

        var prop = typeof(RoomGameManager).GetProperty("CurrentMainState",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop == null) return false;

        object stateObj = prop.GetValue(roomGM, null);
        if (stateObj == null) return false;

        int state;
        if (stateObj is int)
        {
          state = (int)stateObj;
        }
        else if (stateObj.GetType().IsEnum)
        {
          state = Convert.ToInt32(stateObj);
        }
        else if (!int.TryParse(stateObj.ToString(), out state))
        {
          return false;
        }

        return state != 14;
      }
      catch
      {
        return false;
      }
    }
  }
}
