using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityExplorerMcp.Core;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class TakeScreenshotTool : MainThreadTool
    {
        public override string Name => "take_screenshot";
        public override string Description => "Capture the current game screen as a base64-encoded PNG image. Useful for AI to visually understand the current UI layout.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["width"] = new Dictionary<string, object>
                {
                    ["type"] = "number",
                    ["description"] = "Screenshot width (default: current screen width)"
                },
                ["height"] = new Dictionary<string, object>
                {
                    ["type"] = "number",
                    ["description"] = "Screenshot height (default: current screen height)"
                }
            }
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            int width = Screen.width;
            int height = Screen.height;

            if (args != null)
            {
                if (args.TryGetValue("width", out var wv))
                {
                    if (wv is double wd) width = (int)wd;
                    else if (wv is long wl) width = (int)wl;
                }
                if (args.TryGetValue("height", out var hv))
                {
                    if (hv is double hd) height = (int)hd;
                    else if (hv is long hl) height = (int)hl;
                }
            }

            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null)
                return new McpContentItem { type = "text", text = "{\"error\":\"Failed to capture screenshot\"}" };

            byte[] pngBytes = ImageConversion.EncodeToPNG(tex);
            UnityEngine.Object.DestroyImmediate(tex);

            if (pngBytes == null || pngBytes.Length == 0)
                return new McpContentItem { type = "text", text = "{\"error\":\"Failed to encode screenshot\"}" };

            string base64 = Convert.ToBase64String(pngBytes);
            return new McpContentItem
            {
                type = "image",
                data = base64,
                mimeType = "image/png"
            };
        }
    }
}
