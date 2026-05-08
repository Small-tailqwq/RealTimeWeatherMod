using System.Collections.Generic;
using UnityExplorerMcp.Core;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class SearchElementsTool : MainThreadTool
    {
        public override string Name => "search_elements";
        public override string Description => "Search UI elements by name and optional component type filter. Returns matching elements with their paths and basic info.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Search term (case-insensitive name match)"
                },
                ["componentType"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Optional component type filter (e.g., 'Button', 'Image', 'Text')"
                }
            },
            ["required"] = new List<string> { "query" }
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            if (args == null || !args.TryGetValue("query", out var queryVal))
                return new McpContentItem { type = "text", text = "{\"error\":\"Missing 'query' argument\"}" };

            string query = queryVal?.ToString();
            string componentType = args.TryGetValue("componentType", out var ct) ? ct?.ToString() : null;

            var results = UiHierarchyTraverser.SearchElements(query, componentType);
            var json = MCP.JsonHelper.Serialize(results);
            return new McpContentItem { type = "text", text = json };
        }
    }
}
