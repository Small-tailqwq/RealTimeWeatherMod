using System;
using System.Collections.Generic;
using UnityExplorerMcp.Core;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class ExecuteCodeTool : MainThreadTool
    {
        public override string Name => "execute_code";
        public override string Description => "Execute C# code in the game's context and return the result as a string. Uses Mono.CSharp (REPL-style evaluator). Supports System.Linq, UnityEngine, UnityEngine.UI, and Assembly-CSharp.<cr>Examples:<cr>- GameObject.FindObjectsOfType<Button>().Length<cr>- GameObject.Find(\"Canvas\").transform.position.ToString()<cr>- new { x = 1, y = 2 }";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["code"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "C# expression to evaluate"
                }
            },
            ["required"] = new List<string> { "code" }
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            if (args == null || !args.TryGetValue("code", out var codeVal))
                return new McpContentItem { type = "text", text = "{\"error\":\"Missing 'code' argument\"}" };

            string code = codeVal?.ToString();
            if (string.IsNullOrEmpty(code))
                return new McpContentItem { type = "text", text = "{\"error\":\"Empty code\"}" };

            try
            {
                var result = MonoCSharpEvaluator.Execute(code);
                return new McpContentItem
                {
                    type = "text",
                    text = MCP.JsonHelper.Serialize(new { success = true, result = result })
                };
            }
            catch (Exception ex)
            {
                return new McpContentItem
                {
                    type = "text",
                    text = MCP.JsonHelper.Serialize(new { success = false, error = ex.Message })
                };
            }
        }
    }
}
