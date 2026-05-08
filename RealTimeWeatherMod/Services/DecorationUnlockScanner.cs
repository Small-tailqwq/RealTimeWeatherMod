using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ChillWithYou.EnvSync.Services
{
    internal static class DecorationUnlockScanner
    {
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<UnlockGroupAccessor> CachedDecorationAccessors = new List<UnlockGroupAccessor>();

        internal static int Unlock(object service, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            if (service == null)
            {
                return 0;
            }

            var processedOwners = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var processedDictionaries = new HashSet<object>(ReferenceEqualityComparer.Instance);
            int totalUnlocked = 0;

            TryPreRegisterEnvironmentGroup(service, processedOwners, processedDictionaries);
            totalUnlocked += ApplyCachedAccessors(service, processedOwners, processedDictionaries, logInfo, logError, debugMode);
            totalUnlocked += ApplyKnownAccessors(service, processedOwners, processedDictionaries, logInfo, logError, debugMode);
            totalUnlocked += DiscoverFallbackAccessors(service, processedOwners, processedDictionaries, logInfo, logError, debugMode);

            return totalUnlocked;
        }

        private static int ApplyCachedAccessors(object service, HashSet<object> processedOwners, HashSet<object> processedDictionaries, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            int totalUnlocked = 0;
            var stale = new List<UnlockGroupAccessor>();

            foreach (UnlockGroupAccessor accessor in CachedDecorationAccessors)
            {
                if (!TryResolveDictionary(service, accessor, processedOwners, processedDictionaries, out IDictionary dict))
                {
                    stale.Add(accessor);
                    continue;
                }

                totalUnlocked += ApplyUnlockToGroup(dict, accessor.DisplayName, logInfo, logError, debugMode);
            }

            foreach (UnlockGroupAccessor accessor in stale)
            {
                CachedDecorationAccessors.Remove(accessor);
            }

            return totalUnlocked;
        }

        private static int ApplyKnownAccessors(object service, HashSet<object> processedOwners, HashSet<object> processedDictionaries, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            int totalUnlocked = 0;
            UnlockGroupAccessor decorationAccessor = CreateAccessor(service.GetType(), "_decoration", "_skinDic", "_decoration -> _skinDic");
            if (decorationAccessor == null)
            {
                return 0;
            }

            if (!TryResolveDictionary(service, decorationAccessor, processedOwners, processedDictionaries, out IDictionary dict))
            {
                return 0;
            }

            totalUnlocked += ApplyUnlockToGroup(dict, decorationAccessor.DisplayName, logInfo, logError, debugMode);
            return totalUnlocked;
        }

        private static int DiscoverFallbackAccessors(object service, HashSet<object> processedOwners, HashSet<object> processedDictionaries, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
            int totalUnlocked = 0;

            foreach (FieldInfo rootField in service.GetType().GetFields(InstanceFlags))
            {
                if (ShouldSkipRootField(rootField))
                {
                    continue;
                }

                object owner = rootField.GetValue(service);
                if (owner == null || processedOwners.Contains(owner))
                {
                    continue;
                }

                processedOwners.Add(owner);

                foreach (FieldInfo dictField in owner.GetType().GetFields(InstanceFlags))
                {
                    if (!typeof(IDictionary).IsAssignableFrom(dictField.FieldType))
                    {
                        continue;
                    }

                    IDictionary dict = dictField.GetValue(owner) as IDictionary;
                    if (dict == null || dict.Count == 0 || processedDictionaries.Contains(dict))
                    {
                        continue;
                    }

                    if (!TryProbeUnlockShape(dict))
                    {
                        continue;
                    }

                    processedDictionaries.Add(dict);

                    UnlockGroupAccessor accessor = new UnlockGroupAccessor(rootField, dictField, rootField.Name + " -> " + dictField.Name);
                    if (!ContainsAccessor(accessor))
                    {
                        CachedDecorationAccessors.Add(accessor);
                        logInfo?.Invoke("[发现] 记录访问器: " + accessor.DisplayName);
                    }

                    totalUnlocked += ApplyUnlockToGroup(dict, accessor.DisplayName, logInfo, logError, debugMode);
                }
            }

            return totalUnlocked;
        }

        private static bool TryResolveDictionary(object service, UnlockGroupAccessor accessor, HashSet<object> processedOwners, HashSet<object> processedDictionaries, out IDictionary dict)
        {
            dict = null;

            try
            {
                object owner = accessor.RootField.GetValue(service);
                if (owner == null)
                {
                    return false;
                }

                dict = accessor.DictField.GetValue(owner) as IDictionary;
                if (dict == null || dict.Count == 0)
                {
                    return false;
                }

                processedOwners.Add(owner);
                processedDictionaries.Add(dict);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryPreRegisterEnvironmentGroup(object service, HashSet<object> processedOwners, HashSet<object> processedDictionaries)
        {
            FieldInfo environmentField = service.GetType().GetField("_environment", InstanceFlags);
            if (environmentField == null)
            {
                return;
            }

            object environment = environmentField.GetValue(service);
            if (environment == null)
            {
                return;
            }

            FieldInfo environmentDicField = environment.GetType().GetField("_environmentDic", InstanceFlags);
            IDictionary dict = environmentDicField != null ? environmentDicField.GetValue(environment) as IDictionary : null;
            if (dict == null)
            {
                return;
            }

            processedOwners.Add(environment);
            processedDictionaries.Add(dict);
        }

        private static UnlockGroupAccessor CreateAccessor(Type serviceType, string rootFieldName, string dictFieldName, string displayName)
        {
            FieldInfo rootField = serviceType.GetField(rootFieldName, InstanceFlags);
            if (rootField == null)
            {
                return null;
            }

            Type ownerType = rootField.FieldType;
            FieldInfo dictField = ownerType.GetField(dictFieldName, InstanceFlags);
            if (dictField == null)
            {
                return null;
            }

            return new UnlockGroupAccessor(rootField, dictField, displayName);
        }

        private static bool TryProbeUnlockShape(IDictionary dict)
        {
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
                if (valueProperty != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int ApplyUnlockToGroup(IDictionary dict, string displayName, Action<string> logInfo, Action<string> logError, bool debugMode)
        {
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

        private static bool ShouldSkipRootField(FieldInfo field)
        {
            return field.FieldType.Name.Contains("MasterData") || field.FieldType.Name.Contains("Loader");
        }

        private static bool ContainsAccessor(UnlockGroupAccessor candidate)
        {
            foreach (UnlockGroupAccessor accessor in CachedDecorationAccessors)
            {
                if (accessor.RootField.Name == candidate.RootField.Name && accessor.DictField.Name == candidate.DictField.Name)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class UnlockGroupAccessor
        {
            public UnlockGroupAccessor(FieldInfo rootField, FieldInfo dictField, string displayName)
            {
                RootField = rootField;
                DictField = dictField;
                DisplayName = displayName;
            }

            public FieldInfo RootField { get; private set; }
            public FieldInfo DictField { get; private set; }
            public string DisplayName { get; private set; }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
