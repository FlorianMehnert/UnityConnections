using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor.Utils
{
    public abstract class NodeLayoutManager
    {
        private const float DefaultNodeWidth = 100.0f;
        private const float DefaultNodeHeight = 200.0f;
        private const float HorizontalSpacing = 50.0f;
        private const float VerticalSpacing = 50.0f;
        private const float InitialX = 100.0f;
        private const float InitialY = 100.0f;

        private static readonly Dictionary<Node, Vector2> NodeDimensions = new();
        private static Dictionary<Node, bool> NodeReceivedDimension => new();
        private static bool _layoutUpdated;

        // Physics parameters
        private const float AttractionStrength = 0.0005f;
        private const float RepulsionStrength = 1.0f;
        private const float DampingFactor = 0.99f; // Reduces force over time to stabilize layout
        private const int SimulationSteps = 10;
        private const int CollisionRadius = 5;

        public static void LayoutNodes(List<Node> nodes, bool silent = false)
        {
            if (nodes == null || nodes.Count == 0)
                return;

            NodeDimensions.Clear();
            foreach (var node in nodes)
            {
                NodeReceivedDimension[node] = false;
                node.RegisterCallback<GeometryChangedEvent>(_ => OnNodeGeometryChanged(node, nodes, silent));
                _layoutUpdated = false;
            }
        }

        /// <summary>
        /// Nodes will execute this method once they receive the <see cref="GeometryChangedEvent"/> in <see cref="LayoutNodes"/>, unregister the Event for all known nodes and update the layout with now known node sizes.
        /// </summary>
        /// <param name="node">Node receiving the geometry changed event</param>
        /// <param name="nodes">All known nodes</param>
        /// <param name="silent">Whether to not log</param>
        private static void OnNodeGeometryChanged(Node node, List<Node> nodes, bool silent)
        {
            var rect = node.GetPosition();
            var nodeSize = new Vector2(rect.width, rect.height);

            if (nodeSize is { x: > 0, y: > 0 })
            {
                NodeDimensions[node] = nodeSize;
                NodeReceivedDimension[node] = true;
            }

            if (NodeDimensions.Count != nodes.Count) return;

            foreach (var n in nodes)
            {
                n.UnregisterCallback<GeometryChangedEvent>(_ => OnNodeGeometryChanged(n, nodes, silent));
            }

            if (!NodeReceivedDimension.All(kvp => kvp.Value) || _layoutUpdated) return;
            _layoutUpdated = true;
            PerformLayout(nodes, silent);
        }

        /// <summary>
        /// Gridlayout application, requiring avaialble node sizes which need to be waited for using the <see cref="GeometryChangedEvent"/>
        /// </summary>
        /// <param name="nodes">Nodes to perform the layout on</param>
        /// <param name="silent">Whether to not log</param>
        private static void PerformLayout(List<Node> nodes, bool silent)
        {
            // Default grid layout
            var totalNodes = nodes.Count;
            var gridColumns = CalculateOptimalColumnCount(totalNodes);
            var gridRows = Mathf.CeilToInt((float)totalNodes / gridColumns);
            var maxNodeDimensions = GetMaxNodeDimensions();

            if (!silent)
            {
                Debug.Log($"Grid: {gridRows}x{gridColumns}, Total Nodes: {totalNodes}");
                Debug.Log($"Max Node Dimensions: {maxNodeDimensions}");
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var row = i / gridColumns;
                var col = i % gridColumns;

                var position = new Vector2(
                    InitialX + col * (maxNodeDimensions.x + HorizontalSpacing),
                    InitialY + row * (maxNodeDimensions.y + VerticalSpacing)
                );

                SetNodePosition(nodes[i], position, maxNodeDimensions);
            }
        }

        /// <summary>
        /// Calculate Columns for grid based layout with evenly spaced items
        /// </summary>
        /// <param name="nodeCount">Amount of nodes to be played</param>
        /// <returns></returns>
        private static int CalculateOptimalColumnCount(int nodeCount)
        {
            const float targetAspectRatio = 1.618f;
            var columns = Mathf.RoundToInt(Mathf.Sqrt(nodeCount * targetAspectRatio));
            return Mathf.Max(1, columns);
        }

        private static Vector2 GetMaxNodeDimensions()
        {
            var maxWidth = DefaultNodeWidth;
            var maxHeight = DefaultNodeHeight;

            foreach (var size in NodeDimensions.Values)
            {
                maxWidth = Mathf.Max(maxWidth, size.x);
                maxHeight = Mathf.Max(maxHeight, size.y);
            }

            return new Vector2(maxWidth, maxHeight);
        }

        private static void SetNodePosition(Node node, Vector2 position, Vector2 standardSize)
        {
            var width = NodeDimensions.TryGetValue(node, out var dimension) ? dimension.x : standardSize.x;
            var height = NodeDimensions.TryGetValue(node, out var nodeDimension) ? nodeDimension.y : standardSize.y;

            var newRect = new Rect(position, new Vector2(width, height));
            node.SetPosition(newRect);
        }

        private static List<Node> GetConnectedNodes(Node node, List<Edge> allEdges)
        {
            var connectedNodes = new List<Node>();

            // Find all edges that involve the current node
            foreach (var edge in allEdges)
            {
                if (edge.input.node == node && edge.output.node != null)
                    connectedNodes.Add(edge.output.node);
                else if (edge.output.node == node && edge.input.node != null)
                    connectedNodes.Add(edge.input.node);
            }

            return connectedNodes;
        }

        /// <summary>
        /// Method to disable nodes without connections and recalculate layout
        /// </summary>
        /// <param name="nodes"></param>
        /// <param name="allEdges"></param>
        /// <param name="silent"></param>
        public static void DisableDisconnectedNodes(List<Node> nodes, List<Edge> allEdges, bool silent = false)
        {
            var connectedNodes = nodes
                .Where(node => GetConnectedNodes(node, allEdges).Count > 0)
                .ToList();

            // Disable all nodes without connections
            foreach (var node in nodes)
            {
                node.visible = connectedNodes.Contains(node);
            }

            // Recalculate layout with only connected nodes
            PerformLayout(connectedNodes, silent);
        }

        // New physics-based layout method
        [UsedImplicitly]
        public static void PhysicsBasedLayout(List<Node> nodes, List<Edge> allEdges, bool silent = false)
        {
            // Initialize positions and velocities for nodes
            var positions = nodes.ToDictionary(node => node, _ => Random.insideUnitCircle * 100);
            var velocities = nodes.ToDictionary(node => node, _ => Vector2.zero);

            var step = 0;
            for (; step < SimulationSteps; step++)
            {
                // Apply forces for each pair of nodes (repulsion)
                for (var i = 0; i < nodes.Count; i++)
                {
                    for (var j = i + 1; j < nodes.Count; j++)
                    {
                        var nodeA = nodes[i];
                        var nodeB = nodes[j];
                        var delta = positions[nodeB] - positions[nodeA];
                        var distance = delta.magnitude;
                        if (!(distance > 0)) continue;
                        var repulsionForce = RepulsionStrength / (distance * distance);
                        var repulsion = delta.normalized * repulsionForce;
                        velocities[nodeA] -= repulsion;
                        velocities[nodeB] += repulsion;
                    }
                }

                // Apply attraction for connected nodes (edges)
                foreach (var edge in allEdges)
                {
                    if (edge.input.node == null || edge.output.node == null)
                        continue;

                    var nodeA = edge.input.node;
                    var nodeB = edge.output.node;
                    var delta = positions[nodeB] - positions[nodeA];
                    var distance = delta.magnitude;

                    if (!(distance > 0)) continue;
                    var attractionForce = AttractionStrength * distance;
                    var attraction = delta.normalized * attractionForce;
                    velocities[nodeA] += attraction;
                    velocities[nodeB] -= attraction;
                }

                // Update positions and apply damping
                foreach (var node in nodes)
                {
                    velocities[node] *= DampingFactor;
                    positions[node] += velocities[node];
                }
            }

            // Set the final positions
            foreach (var node in nodes)
            {
                var finalPosition = positions[node];
                SetNodePosition(node, finalPosition, GetMaxNodeDimensions());
            }

            if (!silent)
            {
                Debug.Log("Physics-based layout applied.");
            }
        }

        public static void PhysicsBasedLayoutParallel(List<Node> nodes, List<Edge> allEdges, bool silent = false)
        {
            var positions = nodes.ToDictionary(node => node, _ => Random.insideUnitCircle * 100);
            var velocities = nodes.ToDictionary(node => node, _ => Vector2.zero);

            // Use Parallel.For to distribute the simulation steps across multiple threads
            Parallel.For((long)0, SimulationSteps, _ =>
            {
                // Apply forces for each pair of nodes (repulsion and collision)
                Parallel.ForEach(nodes, nodeA =>
                {
                    foreach (var nodeB in nodes)
                    {
                        if (nodeA == nodeB) continue;
                        var delta = positions[nodeB] - positions[nodeA];
                        var distance = delta.magnitude;
                        if (!(distance > 0)) continue;
                        // Repulsion force
                        var repulsionForce = RepulsionStrength / (distance * distance);
                        var repulsion = delta.normalized * repulsionForce;
                        velocities[nodeA] -= repulsion;

                        // Collision avoidance force
                        if (!(distance < CollisionRadius)) continue;
                        var collisionForce = (CollisionRadius - distance) * 10.0f;
                        var collision = delta.normalized * collisionForce;
                        velocities[nodeA] -= collision;
                        velocities[nodeB] += collision;
                    }
                });

                // Apply attraction for connected nodes (edges)
                foreach (var edge in allEdges)
                {
                    if (edge.input.node == null || edge.output.node == null)
                        continue;

                    var nodeA = edge.input.node;
                    var nodeB = edge.output.node;
                    var delta = positions[nodeB] - positions[nodeA];
                    var distance = delta.magnitude;

                    if (!(distance > 0)) continue;
                    var attractionForce = AttractionStrength * distance;
                    var attraction = delta.normalized * attractionForce;
                    velocities[nodeA] += attraction;
                    velocities[nodeB] -= attraction;
                }

                // Update positions and apply damping
                Parallel.ForEach(nodes, node =>
                {
                    velocities[node] *= DampingFactor;
                    positions[node] += velocities[node];
                });
            });

            // Set the final positions
            Parallel.ForEach(nodes, node =>
            {
                var finalPosition = positions[node];
                SetNodePosition(node, finalPosition, GetMaxNodeDimensions());
            });

            if (!silent)
            {
                Debug.Log("Optimized physics-based layout with collision avoidance applied.");
            }
        }
    }
}