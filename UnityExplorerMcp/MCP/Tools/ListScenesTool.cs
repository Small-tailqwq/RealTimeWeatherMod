using System.Collections.Generic;

namespace UnityExplorerMcp.MCP.Tools
{
    internal class ListScenesTool : MainThreadTool
    {
        public override string Name => "list_scenes";
        public override string Description => "List all active scenes and their root GameObjects. Useful for understanding the current game state.";
        public override Dictionary<string, object> InputSchema => new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>()
        };

        public override object Execute(Dictionary<string, object> args) => null;

        public override McpContentItem ExecuteOnMain(Dictionary<string, object> args)
        {
            var sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            var scenes = new List<Dictionary<string, object>>();

            for (int i = 0; i < sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                var rootList = new List<string>();
                foreach (var go in rootObjects)
                    rootList.Add(go.name);

                scenes.Add(new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = scene.path,
                    ["rootObjectCount"] = rootObjects.Length,
                    ["rootObjects"] = rootList
                });
            }

            var json = MCP.JsonHelper.Serialize(scenes);
            return new McpContentItem { type = "text", text = json };
        }
    }
}
