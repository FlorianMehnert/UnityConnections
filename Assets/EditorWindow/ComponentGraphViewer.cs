using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Edge = UnityEditor.Experimental.GraphView.Edge;

namespace EditorWindow
{
    public class ComponentGraphViewer : UnityEditor.EditorWindow
    {
        private ComponentGraphView _graphView;
        private bool _isRefreshing;

        [MenuItem("Window/Component Graph Viewer")]
        public static void OpenWindow()
        {
            ComponentGraphViewer window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Component Graph");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _graphView = new ComponentGraphView(this);
            rootVisualElement.Add(_graphView);

            var refreshButton = new Button(() =>
            {
                if (!_isRefreshing)
                {
                    _isRefreshing = true;
                    EditorApplication.delayCall += () =>
                    {
                        _graphView.RefreshGraph();
                        _isRefreshing = false;
                    };
                }
            }) { text = "Refresh Graph" };
            rootVisualElement.Add(refreshButton);
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }
    }

    public class ComponentGraphView : GraphView
    {
        private readonly Dictionary<System.Type, HashSet<System.Type>> _componentRelations = new Dictionary<System.Type, HashSet<System.Type>>();

        public ComponentGraphView(ComponentGraphViewer window)
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            AddMiniMap();

            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();

            style.flexGrow = 1;
            style.flexShrink = 1;
        }

        public void RefreshGraph()
        {
            PrecomputeComponentRelations();
            CreateComponentGraph();
        }

        private void PrecomputeComponentRelations()
        {
            _componentRelations.Clear();
            var componentTypes = TypeCache.GetTypesDerivedFrom<Component>();

            foreach (var type in componentTypes)
            {
                if (!_componentRelations.ContainsKey(type))
                {
                    _componentRelations[type] = new HashSet<System.Type>();
                }

                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        _componentRelations[type].Add(field.FieldType);
                    }
                }

                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var property in properties)
                {
                    if (typeof(Component).IsAssignableFrom(property.PropertyType))
                    {
                        _componentRelations[type].Add(property.PropertyType);
                    }
                }

                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var method in methods)
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        if (typeof(Component).IsAssignableFrom(parameter.ParameterType))
                        {
                            _componentRelations[type].Add(parameter.ParameterType);
                        }
                    }
                }
            }
        }

        private void CreateComponentGraph()
        {
            DeleteElements(graphElements.ToList());

            Dictionary<System.Type, Node> typeNodes = new Dictionary<System.Type, Node>();
            HashSet<(System.Type, System.Type)> addedEdges = new HashSet<(System.Type, System.Type)>();

            var componentTypes = TypeCache.GetTypesDerivedFrom<Component>().Where(t => !t.IsAbstract);

            foreach (var type in componentTypes)
            {
                Node node = CreateComponentNode(type);
                AddElement(node);
                typeNodes[type] = node;
            }

            foreach (var kvp in _componentRelations)
            {
                if (typeNodes.TryGetValue(kvp.Key, out Node sourceNode))
                {
                    foreach (var relatedType in kvp.Value)
                    {
                        if (typeNodes.TryGetValue(relatedType, out Node targetNode))
                        {
                            var edgeKey = (kvp.Key, relatedType);
                            if (!addedEdges.Contains(edgeKey))
                            {
                                CreateEdge(sourceNode, targetNode);
                                addedEdges.Add(edgeKey);
                            }
                        }
                    }
                }
            }

            LayoutNodes(typeNodes.Values.ToList());
            FrameAll();
        }

        private void CreateEdge(Node sourceNode, Node targetNode)
        {
            Edge edge = new Edge
            {
                output = sourceNode.outputContainer[0] as Port,
                input = targetNode.inputContainer[0] as Port
            };
            edge.input.Connect(edge);
            edge.output.Connect(edge);
            AddElement(edge);
        }

        private Node CreateComponentNode(System.Type componentType)
        {
            var node = new Node
            {
                title = componentType.Name,
                userData = componentType
            };

            var inputPort = GeneratePort(node, Direction.Input, Port.Capacity.Multi);
            node.inputContainer.Add(inputPort);

            var outputPort = GeneratePort(node, Direction.Output, Port.Capacity.Multi);
            node.outputContainer.Add(outputPort);

            return node;
        }

        private Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
        }

        private void LayoutNodes(List<Node> nodes)
        {
            if (nodes.Count == 0) return;

            float padding = 20f;
            float nodeWidth = 400f;
            float nodeHeight = 100f;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));
            int rows = Mathf.CeilToInt((float)nodes.Count / cols);

            for (int i = 0; i < nodes.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;

                float xPos = col * (nodeWidth + padding);
                float yPos = row * (nodeHeight + padding);

                nodes[i].SetPosition(new Rect(xPos, yPos, nodeWidth, nodeHeight));
            }
        }

        private void AddMiniMap()
        {
            MiniMap minimap = new MiniMap()
            {
                anchored = true
            };
            minimap.SetPosition(new Rect(15,50, 200, 100));
            Add(minimap);
        }
    }
}