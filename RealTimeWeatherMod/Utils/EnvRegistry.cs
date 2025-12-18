using System.Collections.Generic;
using Bulbul;

namespace ChillWithYou.EnvSync.Utils
{
    internal static class EnvRegistry
    {
        private static readonly Dictionary<EnvironmentType, EnvironmentController> _map = new Dictionary<EnvironmentType, EnvironmentController>();
        public static bool TryGet(EnvironmentType type, out EnvironmentController ctrl) => _map.TryGetValue(type, out ctrl);
        public static void Register(EnvironmentType type, EnvironmentController ctrl) { if (ctrl != null) _map[type] = ctrl; }
        public static int Count => _map.Count;
    }
}