using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Bulbul;

namespace ChillWithYou.EnvSync.Core
{
    public partial class SceneryAutomationSystem
    {
        private static readonly object _userDataLock = new object();
        private static string _userDataPath;

        private static string GetUserDataPath()
        {
            if (_userDataPath == null)
            {
                _userDataPath = Path.Combine(Paths.ConfigPath, "chillwithyou.envsync.userdata");
            }
            return _userDataPath;
        }

        internal static bool LoadUserInteractedMods()
        {
            try
            {
                var path = GetUserDataPath();
                if (!File.Exists(path)) return true;

                var content = File.ReadAllText(path);
                var parsed = AutoManagedStateCodec.Parse(content);
                lock (_userDataLock)
                {
                    foreach (string name in parsed)
                    {
                        if (Enum.TryParse(name, out EnvironmentType env))
                        {
                            UserInteractedMods.Add(env);
                        }
                    }
                }

                int count;
                lock (_userDataLock)
                {
                    count = UserInteractedMods.Count;
                }
                if (count > 0)
                {
                    ChillEnvPlugin.Log?.LogInfo(
                        $"[用户数据] 已恢复 {count} 项用户操作记录");
                }
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[用户数据] 读取失败: {ex}");
                return false;
            }
        }

        internal static bool PersistUserInteractedMods()
        {
            try
            {
                var names = new List<string>();
                lock (_userDataLock)
                {
                    foreach (EnvironmentType env in UserInteractedMods)
                    {
                        names.Add(env.ToString());
                    }
                }

                var path = GetUserDataPath();
                var tempPath = path + ".tmp";
                var backupPath = path + ".bak";
                File.WriteAllText(tempPath, AutoManagedStateCodec.Serialize(names));
                File.Replace(tempPath, path, backupPath);
                try { File.Delete(backupPath); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                ChillEnvPlugin.Log?.LogWarning($"[用户数据] 写入失败: {ex}");
                return false;
            }
        }
    }
}
