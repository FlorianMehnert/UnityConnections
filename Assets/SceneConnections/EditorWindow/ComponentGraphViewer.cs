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

        [MenuItem("Window/Enhanced Instance-based Component Graph Viewer")]
        public static void OpenWindow()
        {
            ComponentGraphViewer window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Enhanced Component Graph");
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

            // Add component properties to the node
            AddComponentProperties(node, component);

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

        private void AddComponentProperties(Node node, Component component)
        {
            var properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && !p.GetIndexParameters().Any());

            foreach (var property in properties.Take(5)) // Limit to 5 properties to avoid cluttering
            {
                try
                {
                    var value = property.GetValue(component);
                    if (value != null)
                    {
                        var propertyLabel = new Label($"{property.Name}: {value}");
                        node.mainContainer.Add(propertyLabel);
                    }
                }
                catch
                {
                    // Ignore properties that throw exceptions when accessed
                }
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
            // Sort groups by hierarchy depth to layout parent objects before children
            var sortedGroups = _gameObjectGroups.OrderBy(kvp => kvp.Key.transform.GetHierarchyDepth());

            float x = 0;
            float y = 0;
            float maxHeightInRow = 0;
            float padding = 20;
            float maxWidth = 1000; // Adjust based on your needs

            foreach (var kvp in sortedGroups)
            {
                var group = kvp.Value;
                group.UpdateGeometryFromContent();

                // Check if the group exceeds the row width
                if (x + group.contentRect.width > maxWidth)
                {
                    // Move to the next row
                    x = 0;
                    y += maxHeightInRow + padding;
                    maxHeightInRow = 0;
                }

                // Set the position of the group
                group.SetPosition(new Rect(x, y, group.contentRect.width, group.contentRect.height));

                // Update x and maxHeightInRow for the next group
                x += group.contentRect.width + padding;
                maxHeightInRow = Mathf.Max(maxHeightInRow, group.contentRect.height);

                // Layout nodes within the group
                LayoutNodesInGroup(group);
            }
        }


        private void LayoutNodesInGroup(Group group)
        {
            float x = 10;
            float y = 30; // Start below the group title
            float maxHeightInRow = 0;
            float padding = 10;
            float maxWidth = group.contentRect.width - 20; // Leave some margin

            foreach (var element in group.containedElements)
            {
                if (element is Node node)
                {
                    if (x + node.contentRect.width > maxWidth)
                    {
                        x = 10;
                        y += maxHeightInRow + padding;
                        maxHeightInRow = 0;
                    }

                    node.SetPosition(new Rect(x, y, node.contentRect.width, node.contentRect.height));

                    x += node.contentRect.width + padding;
                    maxHeightInRow = Mathf.Max(maxHeightInRow, node.contentRect.height);
                }
            }

            // Update group size to fit all nodes
            group.SetPosition(new Rect(group.contentRect.x, group.contentRect.y, 
                group.contentRect.width, y + maxHeightInRow + padding));
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