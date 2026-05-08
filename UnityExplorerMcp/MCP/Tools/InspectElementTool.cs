using System.Collections.Generic;
using UnityExplorerMcp.Core;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class InspectElementTool : MainThreadTool
    {
        public override string Name => "inspect_element";
        public override string Description => "Get detailed component information for a specific UI element by path. Returns all components, their enabled state, and key property values.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["path"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Full path to the GameObject (e.g., 'Canvas/Panel/Button') or its name"
                }
            },
            ["required"] = new List<string> { "path" }
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            if (args == null || !args.TryGetValue("path", out var pathVal))
                return new McpContentItem { type = "text", text = "{\"error\":\"Missing 'path' argument\"}" };

            string path = pathVal?.ToString();
            var components = UiHierarchyTraverser.GetComponentDetails(path);
            if (components == null)
                return new McpContentItem { type = "text", text = "{\"error\":\"GameObject not found\"}" };

            var element = UiHierarchyTraverser.GetElementByPath(path);
            var json = MCP.JsonHelper.Serialize(new { element, components });
            return new McpContentItem { type = "text", text = json };
        }
    }
}
