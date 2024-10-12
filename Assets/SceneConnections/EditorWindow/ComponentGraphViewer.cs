using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Edge = UnityEditor.Experimental.GraphView.Edge;

namespace SceneConnections.EditorWindow
{
    public class ComponentGraphViewer : UnityEditor.EditorWindow
    {
        private ComponentGraphView _graphView;
        private bool _isRefreshing;

        [MenuItem("Window/Instance-based Component Graph Viewer")]
        public static void OpenWindow()
        {
            ComponentGraphViewer window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Instance-based Component Graph");
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
        private Dictionary<Component, Node> _componentNodes = new Dictionary<Component, Node>();
        private Dictionary<GameObject, Group> _gameObjectGroups = new Dictionary<GameObject, Group>();

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
            ClearGraph();
            CreateComponentGraph();
        }

        private void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
            _componentNodes.Clear();
            _gameObjectGroups.Clear();
        }

        private void CreateComponentGraph()
        {
            var allGameObjects = GameObject.FindObjectsOfType<GameObject>();

            foreach (var gameObject in allGameObjects)
            {
                CreateGameObjectGroup(gameObject);

                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    CreateComponentNode(component);
                }
            }

            CreateEdges();
            LayoutNodes();
        }

        private void CreateGameObjectGroup(GameObject gameObject)
        {
            var group = new Group
            {
                title = gameObject.name,
                userData = gameObject
            };
            AddElement(group);
            _gameObjectGroups[gameObject] = group;
        }

        private void CreateComponentNode(Component component)
        {
            var node = new Node
            {
                title = component.GetType().Name,
                userData = component
            };

            var inputPort = GeneratePort(node, Direction.Input, Port.Capacity.Multi);
            node.inputContainer.Add(inputPort);

            var outputPort = GeneratePort(node, Direction.Output, Port.Capacity.Multi);
            node.outputContainer.Add(outputPort);

            node.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    Selection.activeObject = component;
                    EditorGUIUtility.PingObject(component);
                    evt.StopPropagation();
                }
            });

            AddElement(node);
            _componentNodes[component] = node;

            if (_gameObjectGroups.TryGetValue(component.gameObject, out var group))
            {
                group.AddElement(node);
            }
        }

        private void CreateEdges()
        {
            foreach (var kvp in _componentNodes)
            {
                var sourceComponent = kvp.Key;
                var sourceNode = kvp.Value;

                var fields = sourceComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        var targetComponent = field.GetValue(sourceComponent) as Component;
                        if (targetComponent != null && _componentNodes.TryGetValue(targetComponent, out var targetNode))
                        {
                            CreateEdge(sourceNode, targetNode);
                        }
                    }
                }
            }
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

        private Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
        }

        private void LayoutNodes()
        {
            foreach (var group in _gameObjectGroups.Values)
            {
                group.UpdateGeometryFromContent();
            }

            // You may want to implement a more sophisticated layout algorithm here
            // For now, we'll just spread out the groups
            float x = 0;
            float y = 0;
            float padding = 50;
            foreach (var group in _gameObjectGroups.Values)
            {
                group.SetPosition(new Rect(x, y, group.contentRect.width, group.contentRect.height));
                x += group.contentRect.width + padding;
                if (x > 1000) // Arbitrary width to wrap
                {
                    x = 0;
                    y += 300; // Arbitrary height for next row
                }
            }
        }

        private void AddMiniMap()
        {
            MiniMap minimap = new MiniMap()
            {
                anchored = true
            };
            minimap.SetPosition(new Rect(15, 50, 200, 100));
            Add(minimap);
        }
    }
}