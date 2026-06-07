using System;
using System.Collections.Generic;
using Bulbul;

namespace ChillWithYou.EnvSync.Core
{
    public partial class SceneryAutomationSystem
    {
        private static readonly object _autoModsLock = new object();
        private static bool _ownershipDirty = false;

        private void RestoreManagedOwnership()
        {
            if (ChillEnvPlugin.Cfg_AutoManagedEnvironments == null)
            {
                return;
            }

            _autoEnabledMods.Clear();

            foreach (string name in AutoManagedStateCodec.Parse(
                ChillEnvPlugin.Cfg_AutoManagedEnvironments.Value))
            {
                EnvironmentType environment;
                if (Enum.TryParse(name, out environment) &&
                    _rules.Exists(rule => rule.EnvType == environment))
                {
                    _autoEnabledMods.Add(environment);
                }
            }

            PersistManagedOwnership();
            if (_autoEnabledMods.Count > 0)
            {
                ChillEnvPlugin.Log?.LogInfo(
                    $"[自动托管] 已恢复 {_autoEnabledMods.Count} 项跨会话托管状态");
            }
        }

        internal static void MarkAutoManaged(EnvironmentType environment)
        {
            lock (_autoModsLock)
            {
                if (_autoEnabledMods.Add(environment))
                {
                    _ownershipDirty = true;
                }
            }
        }

        internal static void ReleaseAutoManaged(EnvironmentType environment)
        {
            lock (_autoModsLock)
            {
                if (_autoEnabledMods.Remove(environment))
                {
                    _ownershipDirty = true;
                }
            }
        }

        private static void PersistManagedOwnership()
        {
            if (ChillEnvPlugin.Cfg_AutoManagedEnvironments == null)
            {
                return;
            }

            var names = new List<string>();
            foreach (EnvironmentType environment in _autoEnabledMods)
            {
                names.Add(environment.ToString());
            }

            ChillEnvPlugin.Cfg_AutoManagedEnvironments.Value =
                AutoManagedStateCodec.Serialize(names);
        }

        internal static void FlushOwnershipIfDirty()
        {
            if (!_ownershipDirty)
            {
                return;
            }

            lock (_autoModsLock)
            {
                if (!_ownershipDirty)
                {
                    return;
                }

                PersistManagedOwnership();
                _ownershipDirty = false;
            }
        }
    }
}
