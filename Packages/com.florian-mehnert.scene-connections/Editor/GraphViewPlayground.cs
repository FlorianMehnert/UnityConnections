using System.Collections.Generic;
using SceneConnections.Editor.Utils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class GraphViewPlayground : GraphView, IConnectionGraphView
    {
        private readonly EdgeBuilder _edgeConnector;

        private int _amountOfNodes;
        private int _batchSize;
        private TextField _pathTextField;

        public GraphViewPlayground(StyleColor highlightColor, StyleColor defaultNodeColor)
        {
            HighlightColor = highlightColor;
            Nodes = new List<Node>();
            DefaultNodeColor = defaultNodeColor;
            GraphElements = graphElements.ToList();
            NodeGraphBuilder = new NodeGraphBuilder(this);
            SetupZoom(.01f, 5.0f);

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();
            style.flexGrow = 1;
            style.flexShrink = 1;

            DrawToolbar();
            NodeGraphBuilder.SetupProgressBar();

            _edgeConnector = new EdgeBuilder(this);
            _edgeConnector.SetupProgressBar();
            _edgeConnector.AddPorts();
            
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    NodeGraphBuilder.BuildGraph();
                    break;
                case true when evt.keyCode == KeyCode.E:
                    _edgeConnector.GenerateRandomEdgesAsync();
                    RefreshNodes();
                    break;
            }
        }

        private void DrawToolbar()
        {
            // Create an IMGUIContainer to host IMGUI-based controls.
            var toolbar = new IMGUIContainer(() =>
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                GUI.enabled = !IsBusy;

                // Slider for Max Nodes
                NodeGraphBuilder.AmountOfNodes =
                    EditorGUILayout.IntSlider("Max Nodes", NodeGraphBuilder.AmountOfNodes, 1, 10000);

                // Slider for Batch Size
                NodeGraphBuilder.BatchSize = EditorGUILayout.IntSlider("Batch Size", NodeGraphBuilder.BatchSize, 1,
                    NodeGraphBuilder.AmountOfNodes);

                // Slider for Edge Count
                _edgeConnector.EdgeCount = EditorGUILayout.IntSlider("Edge Count", _edgeConnector.EdgeCount, 1, 100000);


                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    DeleteElements(graphElements.ToList());
                    NodeGraphBuilder.InitGraphAsync();
                }

                if (NodeGraphBuilder.PerformanceMetrics.Count > 0 &&
                    GUILayout.Button("Export Data", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    NodeGraphBuilder.ExportPerformanceData();
                }

                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();
            });


            toolbar.style.position = Position.Absolute;
            toolbar.style.left = 0;
            toolbar.style.top = 0;
            toolbar.style.right = 0;

            Add(toolbar);

            var uiElementsToolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    position = Position.Absolute,
                    left = 0,
                    right = 0,
                    top = 25 // Offset from IMGUI toolbar height
                }
            };

            _pathTextField = new TextField("Path:")
            {
                isReadOnly = true,
                style =
                {
                    flexGrow = 1
                }
            };
            uiElementsToolbar.Add(_pathTextField);

            var selectPathButton = new Button(OpenPathDialog) { text = "Choose Path" };
            uiElementsToolbar.Add(selectPathButton);

            Add(uiElementsToolbar);
        }

        private void OpenPathDialog()
        {
            // Open the folder selection dialog and store the selected path
            var path = EditorUtility.OpenFolderPanel("Select Path", "", "");

            if (!string.IsNullOrEmpty(path))
            {
                _pathTextField.value = path;
            }
        }

        private void RefreshNodes()
        {
            foreach (var node in nodes)
            {
                node.RefreshExpandedState();
                node.RefreshPorts();
            }
        }

        public StyleColor HighlightColor { get; }
        public List<Node> Nodes { get; set; }
        public StyleColor DefaultNodeColor { get; }
        public string PathTextFieldValue { get; set; }
        public TextField PathTextField { get; set; }
        public TextField SearchField { get; set; }
        public bool IsBusy { get; set; }
        public List<GraphElement> GraphElements { get; }
        public NodeGraphBuilder NodeGraphBuilder { get; }
        public bool ReferenceInheritance { get; set; }
        public bool ReferenceFields { get; set; }
        public bool ReferenceMethods { get; set; }
    }

    public class GraphViewPlaygroundViewer : EditorWindow
    {
        private GraphView _graphView;
        private bool _isRefreshing;

        private void OnEnable()
        {
            // StyleColor highlightColor, List<Node> nodes, StyleColor defaultNodeColor, List<GraphElement> graphElements, NodeGraphBuilder nodeGraphBuilder
            var hightlightColor = new Color(1f, 0.8f, 0.2f, 1f);
            var defaultColor = new Color(0.2f, 0.2f, 0.2f, .5f);

            _graphView = new GraphViewPlayground(hightlightColor, defaultColor);
            rootVisualElement.Add(_graphView);
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }

        [MenuItem("Window/Connections v0 #&0")]
        public static void OpenWindow()
        {
            var window = GetWindow<GraphViewPlaygroundViewer>();
            window.titleContent = new GUIContent("GraphView Playground");
            window.minSize = new Vector2(800, 600);
        }
    }
}