using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;
using Debug = UnityEngine.Debug;
using Edge = UnityEditor.Experimental.GraphView.Edge;

namespace SceneConnections.Editor.Utils
{
    public class EdgeBuilder
    {
        private readonly GraphView _targetGraphView;
        private List<Node> _nodes = new();
        [UsedImplicitly]
        private readonly List<Edge> _edges = new();
        private bool _isProcessing;
        private float _progress;
        private const int BatchSize = 2500; // Default batch size
        public int EdgeCount = 5000;

        // Progress UI elements
        private bool _showProgressBar;
        private IMGUIContainer _progressBar;
        private readonly List<PerformanceMetrics> _performanceMetrics = new();
        private readonly Stopwatch _totalStopwatch = new();

        public EdgeBuilder(GraphView graphView)
        {
            _targetGraphView = graphView;
            RefreshNodesList();
            _isProcessing = false;
        }

        public void SetupProgressBar()
        {
            _progressBar = new IMGUIContainer(() =>
            {
                if (!_showProgressBar) return;

                EditorGUI.ProgressBar(
                    new Rect(210, 27, 450, 17),
                    _progress,
                    $"Generating Edges: {_progress * 100:F1}%"
                );

                if (_totalStopwatch.IsRunning)
                {
                    EditorGUI.LabelField(
                        new Rect(10, 55, 500, 20),
                        $"Elapsed Time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds"
                    );
                }
            });

            _targetGraphView.Add(_progressBar);
            _progressBar.style.position = Position.Absolute;
            _progressBar.style.left = 0;
            _progressBar.style.right = 0;
        }

        private void RefreshNodesList()
        {
            _nodes = _targetGraphView.nodes.ToList();
        }

        public void AddPorts()
        {
            foreach (var node in _targetGraphView.nodes)
            {
                var inputPort = node.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(Component));
                node.inputContainer.Add(inputPort);

                var outputPort = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Component));
                node.outputContainer.Add(outputPort);
            }
        }

        public async void GenerateRandomEdgesAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            _showProgressBar = true;
            _progress = 0;

            _performanceMetrics.Clear();
            _totalStopwatch.Restart();

            // Clear existing edges first
            var existingEdges = _targetGraphView.edges.ToList();
            foreach (var edge in existingEdges)
            {
                _targetGraphView.RemoveElement(edge);
            }

            RefreshNodesList();
            if (_nodes.Count < 2)
            {
                _isProcessing = false;
                _showProgressBar = false;
                _progressBar?.MarkDirtyRepaint();
                return;
            }

            var batches = Mathf.CeilToInt((float)EdgeCount / BatchSize);

            for (var batch = 0; batch < batches; batch++)
            {
                var batchMetrics = new PerformanceMetrics
                {
                    BatchNumber = batch + 1,
                    Timestamp = DateTime.Now
                };

                var start = batch * BatchSize;
                var count = Mathf.Min(BatchSize, EdgeCount - start);
                batchMetrics.EdgesInBatch = count;

                var batchStopwatch = Stopwatch.StartNew();

                // Create batch of edges
                var edgesToAdd = new List<Edge>();
                for (var i = 0; i < count; i++)
                {
                    var edge = CreateRandomEdge();
                    if (edge != null)
                    {
                        edgesToAdd.Add(edge);
                    }
                }

                // Add all edges in the batch to the graph
                foreach (var edge in edgesToAdd)
                {
                    _targetGraphView.AddElement(edge);
                }


                batchStopwatch.Stop();
                batchMetrics.BatchCreationTime = batchStopwatch.Elapsed.TotalMilliseconds;
                _performanceMetrics.Add(batchMetrics);

                _progress = (float)(batch + 1) / batches;
                _progressBar?.MarkDirtyRepaint();

                await Task.Yield();
            }

            _totalStopwatch.Stop();
            ExportPerformanceData(EdgeCount);

            _isProcessing = false;
            _showProgressBar = false;
            _progressBar?.MarkDirtyRepaint();

            Debug.Log($"Total edge generation time: {_totalStopwatch.Elapsed.TotalSeconds:F2} seconds");
        }

        private Edge CreateRandomEdge()
        {
            // Get random nodes
            var outputNode = GetRandomNode();
            var inputNode = GetRandomNode();
            while (inputNode == outputNode)
            {
                inputNode = GetRandomNode();
            }

            return CreateEdge(inputNode, outputNode);
        }

        private Node GetRandomNode()
        {
            var randomIndex = Random.Range(0, _nodes.Count);
            return _nodes[randomIndex];
        }

        private Edge CreateEdge(Node sourceNode, Node targetNode)
        {
            if (sourceNode == targetNode) return null; // Prevent self-referencing edges

            // Check if edge already exists
            if (_edges.Any(e =>
                    (e.output.node == sourceNode && e.input.node == targetNode) ||
                    (e.input.node == sourceNode && e.output.node == targetNode)))
            {
                return null;
            }

            // Create ports if needed
            var outputPort = EnsurePort(sourceNode, Direction.Output);
            var inputPort = EnsurePort(targetNode, Direction.Input);

            var edge = new Edge
            {
                output = outputPort,
                input = inputPort
            };

            edge.input.Connect(edge);
            edge.output.Connect(edge);
            _targetGraphView.AddElement(edge);
            return edge;
        }

        private static Port EnsurePort(Node node, Direction direction)
        {
            var container = direction == Direction.Output ? node.outputContainer : node.inputContainer;

            if (container.childCount != 0) return container[0] as Port;
            var port = node.InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(MonoScript));
            container.Add(port);
            node.RefreshPorts();
            node.RefreshExpandedState();
            return port;
        }

        private void ExportPerformanceData(int totalEdges)
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Edge Generation Performance Data",
                "",
                $"EdgeGenerationPerformance_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                "csv");

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                using (var writer = new StreamWriter(path))
                {
                    // Write header
                    writer.WriteLine("DateTime,BatchNumber,EdgesInBatch,CreationTime(ms)");

                    // Write data
                    foreach (var metric in _performanceMetrics)
                    {
                        writer.WriteLine(
                            $"{metric.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                            $"{metric.BatchNumber}," +
                            $"{metric.EdgesInBatch}," +
                            $"{metric.BatchCreationTime:F2}"
                        );
                    }

                    // Write summary
                    writer.WriteLine();
                    writer.WriteLine("Summary Statistics");
                    writer.WriteLine($"Total Edges,{totalEdges}");
                    writer.WriteLine($"Total Time (seconds),{_totalStopwatch.Elapsed.TotalSeconds:F2}");
                    writer.WriteLine($"Average Time per Edge (ms),{_totalStopwatch.Elapsed.TotalMilliseconds / totalEdges:F2}");
                    writer.WriteLine($"Average Creation Time per Batch (ms),{_performanceMetrics.Average(m => m.BatchCreationTime):F2}");
                }

                Debug.Log($"Edge generation performance data exported to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting performance data: {e.Message}");
            }
        }
    }
}