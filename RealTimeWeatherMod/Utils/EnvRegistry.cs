using System.Collections.Generic;
using Bulbul;

namespace ChillWithYou.EnvSync.Utils
{
    internal static class EnvRegistry
    {
        private static readonly Dictionary<EnvironmentType, EnviromentController> _map = new Dictionary<EnvironmentType, EnviromentController>();
        public static bool TryGet(EnvironmentType type, out EnviromentController ctrl) => _map.TryGetValue(type, out ctrl);
        public static void Register(EnvironmentType type, EnviromentController ctrl) { if (ctrl != null) _map[type] = ctrl; }
        public static int Count => _map.Count;
    }
}