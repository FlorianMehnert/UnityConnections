using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SceneConnections.Editor.Nodes;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SceneConnections.Editor.Utils
{
    public class LayoutState
    {
        public struct NodeLayout
        {
            public GameObjectNode Node;
            public Rect FinalRect;
        }

        public struct GroupLayout
        {
            public Group Group;
            public Rect FinalRect;
            public List<NodeLayout> NodeLayouts;
        }

        public List<GroupLayout> GroupLayouts { get; } = new();

        public void ApplyLayout()
        {
            foreach (var groupLayout in GroupLayouts)
            {
                // Apply group rectangle
                groupLayout.Group.SetPosition(groupLayout.FinalRect);

                // Apply node rectangles
                foreach (var nodeLayout in groupLayout.NodeLayouts)
                {
                    nodeLayout.Node.SetPosition(nodeLayout.FinalRect);
                }
            }
        }

        public static void SetTotalSize()
        {
        }
    }

    public abstract class NodeUtils
    {
        public static LayoutState OptimizeGroupLayouts(Group[] groups, float padding = 500f)
        {
            LayoutState layoutState = new LayoutState();

            if (groups == null || groups.Length == 0)
                return layoutState;

            // First optimize internal layout of each group and store the results
            var groupLayouts = new List<LayoutState.GroupLayout>();

            foreach (var group in groups)
            {
                var groupLayout = new LayoutState.GroupLayout
                {
                    Group = group,
                    NodeLayouts = new List<LayoutState.NodeLayout>(),
                    FinalRect = new Rect(0, 0, 0, 0) // Initialize with zero rect
                };

                if (group.containedElements != null && group.containedElements.Any())
                {
                    List<GameObjectNode> nodes = group.containedElements.OfType<GameObjectNode>().ToList();

                    // Sort nodes by area
                    nodes.Sort((a, b) =>
                    {
                        float areaA = a.contentRect.width * a.contentRect.height;
                        float areaB = b.contentRect.width * b.contentRect.height;
                        return areaB.CompareTo(areaA);
                    });

                    // Calculate optimal node positions within group
                    var rowHeights = new float[Mathf.CeilToInt(Mathf.Sqrt(nodes.Count))];
                    var colWidths = new float[Mathf.CeilToInt((float)nodes.Count / rowHeights.Length)];
                    var nodePositions = CalculateOptimalNodePositions(nodes, padding, rowHeights, colWidths);

                    // Store node layouts with local positions (relative to group)
                    for (var i = 0; i < nodes.Count; i++)
                    {
                        var nodeLayout = new LayoutState.NodeLayout
                        {
                            Node = nodes[i],
                            FinalRect = new Rect(
                                nodePositions[i].x,
                                nodePositions[i].y,
                                nodes[i].contentRect.width,
                                nodes[i].contentRect.height
                            )
                        };
                        groupLayout.NodeLayouts.Add(nodeLayout);
                    }

                    // Calculate group size based on node positions
                    var groupWidth = colWidths.Sum() + padding * (colWidths.Length + 1);
                    var groupHeight = rowHeights.Sum() + padding * (rowHeights.Length + 1);
                    groupLayout.FinalRect.width = groupWidth;
                    groupLayout.FinalRect.height = groupHeight;
                }

                groupLayouts.Add(groupLayout);
            }

            // Sort groups by area for better packing
            groupLayouts.Sort((a, b) =>
            {
                var areaA = a.FinalRect.width * a.FinalRect.height;
                var areaB = b.FinalRect.width * b.FinalRect.height;
                return areaB.CompareTo(areaA);
            });

            // Try different grid arrangements for groups
            var bestTotalArea = float.MaxValue;
            var bestPositions = new Vector2[groupLayouts.Count];

            var maxRows = Mathf.CeilToInt(Mathf.Sqrt(groupLayouts.Count));

            for (var numRows = 1; numRows <= maxRows; numRows++)
            {
                var numCols = Mathf.CeilToInt((float)groupLayouts.Count / numRows);
                var currentPositions = new Vector2[groupLayouts.Count];

                var rowHeights = new float[numRows];
                var colWidths = new float[numCols];

                // Calculate maximum dimensions for each row and column
                for (var i = 0; i < groupLayouts.Count; i++)
                {
                    var row = i / numCols;
                    var col = i % numCols;

                    var rect = groupLayouts[i].FinalRect;
                    rowHeights[row] = Mathf.Max(rowHeights[row], rect.height);
                    colWidths[col] = Mathf.Max(colWidths[col], rect.width);
                }

                // Calculate positions and total size
                var currentY = padding;
                var totalWidth = padding;
                var totalHeight = padding;

                for (var row = 0; row < numRows; row++)
                {
                    var currentX = padding;

                    for (var col = 0; col < numCols; col++)
                    {
                        var index = row * numCols + col;
                        if (index >= groupLayouts.Count) continue;
                        currentPositions[index] = new Vector2(currentX, currentY);
                        currentX += colWidths[col] + padding;
                    }

                    totalWidth = Mathf.Max(totalWidth, currentX);
                    currentY += rowHeights[row] + padding;
                    totalHeight = currentY;
                }

                var totalArea = totalWidth * totalHeight;

                if (!(totalArea < bestTotalArea)) continue;
                bestTotalArea = totalArea;
                bestPositions = currentPositions.ToArray();
            }

            // Create final layout state with updated positions
            var finalGroupLayouts = new List<LayoutState.GroupLayout>();

            for (var i = 0; i < groupLayouts.Count; i++)
            {
                var originalGroupLayout = groupLayouts[i];
                var groupPosition = bestPositions[i];

                // Create new group layout with updated position
                var updatedGroupLayout = new LayoutState.GroupLayout
                {
                    Group = originalGroupLayout.Group,
                    FinalRect = new Rect(
                        groupPosition.x,
                        groupPosition.y,
                        originalGroupLayout.FinalRect.width,
                        originalGroupLayout.FinalRect.height
                    ),
                    NodeLayouts = new List<LayoutState.NodeLayout>()
                };

                // Update node positions relative to new group position
                foreach (var updatedNodeLayout in originalGroupLayout.NodeLayouts.Select(originalNodeLayout => new LayoutState.NodeLayout
                         {
                             Node = originalNodeLayout.Node,
                             FinalRect = new Rect(
                                 groupPosition.x + originalNodeLayout.FinalRect.x,
                                 groupPosition.y + originalNodeLayout.FinalRect.y,
                                 originalNodeLayout.FinalRect.width,
                                 originalNodeLayout.FinalRect.height
                             )
                         }))
                {
                    updatedGroupLayout.NodeLayouts.Add(updatedNodeLayout);
                }

                finalGroupLayouts.Add(updatedGroupLayout);
            }

            layoutState.GroupLayouts.AddRange(finalGroupLayouts);
            LayoutState.SetTotalSize();

            return layoutState;
        }

        private static Vector2[] CalculateOptimalNodePositions(List<GameObjectNode> nodes, float padding, float[] rowHeights, float[] colWidths)
        {
            var positions = new Vector2[nodes.Count];

            // Calculate positions in a grid
            for (var i = 0; i < nodes.Count; i++)
            {
                var row = i / colWidths.Length;
                var col = i % colWidths.Length;

                GameObjectNode node = nodes[i];
                Rect rect = node.contentRect;

                // Update row heights and column widths
                rowHeights[row] = Mathf.Max(rowHeights[row], rect.height);
                colWidths[col] = Mathf.Max(colWidths[col], rect.width);
            }

            // Calculate final positions
            var y = padding;
            for (var row = 0; row < rowHeights.Length; row++)
            {
                var x = padding;
                for (var col = 0; col < colWidths.Length; col++)
                {
                    var index = row * colWidths.Length + col;
                    if (index >= nodes.Count) continue;
                    positions[index] = new Vector2(x, y);
                    x += colWidths[col] + padding;
                }

                y += rowHeights[row] + padding;
            }

            return positions;
        }

        public static void HighlightNode(GraphView graphView, Node node)
        {
            if (node == null) return;
            graphView.AddToSelection(node);
        }

        [CanBeNull]
        public static HashSet<Node> TraverseConnectedNodes(Node node, Color color, HashSet<Node> visitedNodes)
        {
            if (!visitedNodes.Add(node))
                return visitedNodes;

            // Apply color to the node
            node.style.backgroundColor = color;


            // Traverse output connected nodes
            foreach (var port in node.outputContainer.Children().OfType<Port>())
            {
                foreach (var edge in port.connections)
                {
                    if (edge.input.node is { } connectedNode)
                    {
                        TraverseConnectedNodes(connectedNode, color, visitedNodes);
                    }
                }
            }

            // Traverse input connected nodes
            foreach (var port in node.inputContainer.Children().OfType<Port>())
            {
                foreach (var edge in port.connections)
                {
                    if (edge.output.node is { } connectedNode)
                    {
                        TraverseConnectedNodes(connectedNode, color, visitedNodes);
                    }
                }
            }

            return null;
        }

        private static List<Node> GetAllNodesInActiveGraphView()
        {
            // Get the currently focused editor window
            var window = EditorWindow.focusedWindow;
            if (window == null) return null;

            // Look for a GraphView within the editor window's root visual element
            var graphView = window.rootVisualElement.Children().OfType<GraphView>().FirstOrDefault();

            // Return all nodes in the GraphView
            return graphView?.nodes.ToList();
        }

        /// <summary>
        /// Reset the color of all nodes
        /// </summary>
        public static void ResetNodeColors()
        {
            foreach (var node in GetAllNodesInActiveGraphView())
            {
                node.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, .5f);
            }
        }
    }
}