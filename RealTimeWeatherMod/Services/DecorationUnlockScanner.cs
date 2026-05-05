using System;
using System.Collections;
using System.Reflection;

namespace ChillWithYou.EnvSync.Services
{
    internal static class DecorationUnlockScanner
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static int Unlock(object service, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            if (service == null)
            {
                return 0;
            }

            int totalUnlocked = 0;
            Type serviceType = service.GetType();

            FieldInfo decorationField = serviceType.GetField("_decoration", InstanceFlags);
            if (decorationField != null)
            {
                object decoration = decorationField.GetValue(service);
                if (decoration != null)
                {
                    FieldInfo skinDicField = decoration.GetType().GetField("_skinDic", InstanceFlags);
                    IDictionary skinDic = skinDicField != null ? skinDicField.GetValue(decoration) as IDictionary : null;
                    totalUnlocked += UnlockDictionary(skinDic, "_decoration -> _skinDic", logInfo, logError, debugMode);
                }
            }

            foreach (FieldInfo field in serviceType.GetFields(InstanceFlags))
            {
                if (field.FieldType.Name.Contains("MasterData") || field.FieldType.Name.Contains("Loader"))
                {
                    continue;
                }
            }

            return totalUnlocked;
        }

        private static int UnlockDictionary(IDictionary dict, string displayName, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            if (dict == null)
            {
                return 0;
            }

            int unlockedCount = 0;

            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                FieldInfo lockField = entry.Value.GetType().GetField("_isLocked", InstanceFlags);
                if (lockField == null)
                {
                    continue;
                }

                object reactiveBool = lockField.GetValue(entry.Value);
                if (reactiveBool == null)
                {
                    continue;
                }

                PropertyInfo valueProperty = reactiveBool.GetType().GetProperty("Value", InstanceFlags);
                if (valueProperty == null)
                {
                    continue;
                }

                bool isLocked = (bool)valueProperty.GetValue(reactiveBool, null);
                if (!isLocked)
                {
                    continue;
                }

                valueProperty.SetValue(reactiveBool, false, null);
                unlockedCount++;

                if (debugMode)
                {
                    logInfo?.Invoke("   🔓 解锁: " + entry.Key + " (在 " + displayName + ")");
                }
            }

            if (unlockedCount > 0)
            {
                logInfo?.Invoke("✅ 在 " + displayName + " 中解锁了 " + unlockedCount + " 个项目");
            }

            return unlockedCount;
        }
    }
}
