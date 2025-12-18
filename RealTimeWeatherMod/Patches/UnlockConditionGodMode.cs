using HarmonyLib;
using System;
using System.Reflection;
using Bulbul;

namespace ChillWithYou.EnvSync.Patches
{
    /// <summary>
    /// 【上帝模式解锁补丁 - 终极版】
    /// 全面接管 UnlockConditionService。
    /// 1. 拦截 IsUnlocked -> 告诉游戏“已解锁”。
    /// 2. 拦截 IsPurchasableItem -> 告诉游戏“不可购买（已拥有）”，消灭价格标签。
    /// </summary>
    public static class UnlockConditionGodMode
    {
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                ChillEnvPlugin.Log?.LogInfo("🛡️ 正在部署上帝模式 (终极版)...");

                // 1. 获取 Service 类型
                Type serviceType = AccessTools.TypeByName("Bulbul.UnlockConditionService");

                // 2. 动态捕获 Enum 类型 (Bulbul.DecorationService+DecorationSkinType)
                Type skinEnumType = null;
                Type unlockDecoType = AccessTools.TypeByName("UnlockDecoration"); 
                
                if (unlockDecoType != null)
                {
                    MethodInfo purchaseMethod = AccessTools.Method(unlockDecoType, "Purchase");
                    if (purchaseMethod != null)
                    {
                        var parameters = purchaseMethod.GetParameters();
                        if (parameters.Length > 0)
                        {
                            skinEnumType = parameters[0].ParameterType;
                            ChillEnvPlugin.Log?.LogInfo($"✅ 成功捕获 Enum: {skinEnumType.Name}");
                        }
                    }
                }

                if (serviceType == null || skinEnumType == null)
                {
                    ChillEnvPlugin.Log?.LogError("❌ 类型解析失败，补丁取消。");
                    return;
                }

                // =========================================================
                // 3. Patch IsUnlocked<T> (解决锁图标)
                // =========================================================
                MethodInfo isUnlockedOrigin = AccessTools.Method(serviceType, "IsUnlocked")?.MakeGenericMethod(skinEnumType);
                MethodInfo isUnlockedPrefix = typeof(UnlockConditionGodMode).GetMethod(nameof(IsUnlockedPrefix));
                
                if (isUnlockedOrigin != null)
                {
                    harmony.Patch(isUnlockedOrigin, prefix: new HarmonyMethod(isUnlockedPrefix));
                    ChillEnvPlugin.Log?.LogInfo("✅ IsUnlocked 拦截成功");
                }

                // =========================================================
                // 4. Patch IsPurchasableItem<T> (解决价格标签/购买弹窗)
                // =========================================================
                // 目标: public bool IsPurchasableItem<T>(T itemType, out int price)
                MethodInfo isPurchasableOrigin = AccessTools.Method(serviceType, "IsPurchasableItem")?.MakeGenericMethod(skinEnumType);
                MethodInfo isPurchasablePrefix = typeof(UnlockConditionGodMode).GetMethod(nameof(IsPurchasablePrefix));

                if (isPurchasableOrigin != null)
                {
                    harmony.Patch(isPurchasableOrigin, prefix: new HarmonyMethod(isPurchasablePrefix));
                    ChillEnvPlugin.Log?.LogInfo("✅ IsPurchasableItem 拦截成功");
                }
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogError($"❌ 上帝模式部署失败: {ex}");
            }
        }

        /// <summary>
        /// 拦截 IsUnlocked: 强制返回 (true, true) -> 视为已解锁
        /// </summary>
        public static bool IsUnlockedPrefix(ref ValueTuple<bool, bool> __result)
        {
            if (!ChillEnvPlugin.Cfg_UnlockDecorations.Value) return true;
            __result = new ValueTuple<bool, bool>(true, true);
            return false; // 拦截原方法
        }

        /// <summary>
        /// 拦截 IsPurchasableItem: 强制返回 false, price=0 -> 视为不可购买(已拥有)
        /// 注意：这是带 out 参数的方法，需要在 Prefix 里给 out 参数赋值
        /// </summary>
        public static bool IsPurchasablePrefix(ref int price, ref bool __result)
        {
            if (!ChillEnvPlugin.Cfg_UnlockPurchasableItems.Value) return true;
            
            price = 0;        // 价格设为 0
            __result = false; // 返回 false (不可购买)
            
            return false; // 拦截原方法
        }
    }
}