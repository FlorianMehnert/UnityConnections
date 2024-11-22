using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneConnections.Editor.Utils
{
    public static class GraphViewUtilities
    {
        /// <summary>
        /// WIP
        /// </summary>
        /// <param name="graphView"></param>
        public static void CenterCameraOnSelectedNodes(GraphView graphView)
        {
            if (graphView == null)
            {
                Debug.LogWarning("No GraphView or no nodes selected.");
                return;
            }
            var coloredNodes = GetAllNodesWithBackgroundColor(graphView, Color.red);
            if (coloredNodes == null || coloredNodes.Count == 0)
            {
                Debug.LogWarning("No nodes selected to center on.");
                return;
            }
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var pos in coloredNodes.Select(node => node.GetPosition()))
            {
                minX = Mathf.Min(minX, pos.x);
                minY = Mathf.Min(minY, pos.y);
                maxX = Mathf.Max(maxX, pos.x + pos.width);
                maxY = Mathf.Max(maxY, pos.y + pos.height);
            }
            var width = maxX - minX;
            var height = maxY - minY;
            const float padding = 20f;
            width += padding * 2;
            height += padding * 2;
            var viewport = graphView.contentViewContainer.worldBound;
            var centerPosition = new Vector2(minX + width / 2, minY + height / 2);
            var scaleX = viewport.width / width;
            var scaleY = viewport.height / height;
            var scale = Mathf.Min(scaleX, scaleY);
            scale *= 0.9f; // Slight reduction to ensure everything is visible
            scale = Mathf.Clamp(scale, 0.2f, 2f);
            graphView.UpdateViewTransform(-centerPosition, Vector3.one * scale);

        }

        /// <summary>
        /// Retrieves all nodes in the current graph view that have a background color of red
        /// </summary>
        /// <param name="graphView">Graphview containing the nodes</param>
        /// <param name="targetColor">Color from which the nodes will be selected</param>
        /// <returns></returns>
        private static List<Node> GetAllNodesWithBackgroundColor(GraphView graphView, Color targetColor)
        {
            if (graphView != null)
                return graphView.nodes
                    .Where(node => node.style.backgroundColor.value == targetColor)
                    .ToList();
            Debug.LogWarning("GraphView is null, cannot get nodes.");
            return null;
        }
        
        public static void ExportGraphToGraphviz(GraphView graphView, string filePath)
        {
            // Initialize a StringBuilder to create the .dot content
            var dotContent = new StringBuilder();

            // Start the Graphviz dot format with the "digraph" keyword if it's directed, or "graph" if undirected
            dotContent.AppendLine("digraph UnityGraph {");

            // Iterate over nodes in the GraphView
            foreach (var nodeName in graphView.nodes.ToList().Select(node => node.title ?? node.GetType().Name))
            {
                dotContent.AppendLine($"    \"{nodeName}\";");  // Adding each node
            }

            // Iterate over edges in the GraphView
            foreach (var edge in graphView.edges.ToList())
            {
                var outputNode = edge.output.node.title;
                var inputNode = edge.input.node.title;

                // Add each edge in the format "node1" -> "node2"
                dotContent.AppendLine($"    \"{outputNode}\" -> \"{inputNode}\";");
            }

            // Close the graph
            dotContent.AppendLine("}");

            // Write the StringBuilder content to a .dot file
            File.WriteAllText(filePath, dotContent.ToString());
        }
    }
}