using System;

namespace SceneConnections.Editor.Utils
{
    public static class Constants
    {
        public enum ComponentGraphDrawType
        {
            NodesAreComponents = 1,
            NodesAreGameObjects = 2,
            NodesAreScripts = 3
        }

        public static ComponentGraphDrawType ToCgdt(string s)
        {
            return s switch
            {
                "nodes are components" => ComponentGraphDrawType.NodesAreComponents,
                "nodes are game objects" => ComponentGraphDrawType.NodesAreGameObjects,
                "nodes are scripts" => ComponentGraphDrawType.NodesAreScripts,
                _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
            };
        }
    }
}