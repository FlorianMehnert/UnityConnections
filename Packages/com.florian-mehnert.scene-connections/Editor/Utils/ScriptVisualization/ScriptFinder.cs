using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ScriptFinder
    {
        public static List<string> GetAllScriptPaths()
        {
            // Fetch all assets with the `.cs` extension in the project
            var guids = AssetDatabase.FindAssets("t:Script");

            return guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
        }
    }
}