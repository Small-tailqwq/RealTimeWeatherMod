using System;
using System.Collections.Generic;

namespace ChillWithYou.EnvSync.Core
{
    internal static class AutoManagedStateCodec
    {
        internal static HashSet<string> Parse(string serialized)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return result;
            }

            foreach (string item in serialized.Split(','))
            {
                string name = item.Trim();
                if (name.Length > 0)
                {
                    result.Add(name);
                }
            }

            return result;
        }

        internal static string Serialize(IEnumerable<string> names)
        {
            var uniqueNames = new SortedSet<string>(StringComparer.Ordinal);
            if (names != null)
            {
                foreach (string name in names)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        uniqueNames.Add(name.Trim());
                    }
                }
            }

            return string.Join(",", uniqueNames);
        }
    }
}
