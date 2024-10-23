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
            var window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Enhanced Component Graph");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            _graphView = new ComponentGraphView();
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
        private readonly Dictionary<Component, Node> _componentNodes = new();
        private readonly Dictionary<GameObject, Group> _gameObjectGroups = new();

        public ComponentGraphView()
        {
            SetupZoom(.01f, 5.0f);
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
            var allGameObjects = Object.FindObjectsOfType<GameObject>();

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
                if (evt.clickCount != 2) return;
                Selection.activeObject = component;
                EditorGUIUtility.PingObject(component);
                evt.StopPropagation();
            });

            AddElement(node);
            _componentNodes[component] = node;

            if (_gameObjectGroups.TryGetValue(component.gameObject, out var group))
            {
                group.AddElement(node);
            }
        }

        private static void AddComponentProperties(Node node, Component component)
        {
            var properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && !p.GetIndexParameters().Any());

            foreach (var property in properties.Take(5)) // Limit to 5 properties to avoid cluttering
            {
                try
                {
                    var value = property.GetValue(component);
                    if (value == null) continue;
                    var propertyLabel = new Label($"{property.Name}: {value}");
                    node.mainContainer.Add(propertyLabel);
                }
                catch
                {
                    // Ignore properties that throw exceptions when accessed
                }
            }
        }

        private void CreateEdges()
        {
            foreach (var (sourceComponent, sourceNode) in _componentNodes)
            {
                var fields = sourceComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (!typeof(Component).IsAssignableFrom(field.FieldType)) continue;
                    var targetComponent = field.GetValue(sourceComponent) as Component;
                    if (targetComponent != null && _componentNodes.TryGetValue(targetComponent, out var targetNode))
                    {
                        CreateEdge(sourceNode, targetNode);
                    }
                }
            }
        }

        private void CreateEdge(Node sourceNode, Node targetNode)
        {
            var edge = new Edge
            {
                output = sourceNode.outputContainer[0] as Port,
                input = targetNode.inputContainer[0] as Port
            };
            edge.input?.Connect(edge);
            edge.output?.Connect(edge);
            AddElement(edge);
        }

        private Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
        }


        private void LayoutNodes()
        {
            var sortedGroups = _gameObjectGroups; //.OrderBy(kvp => kvp.Key.transform.GetHierarchyDepth());

            float x = 0;
            float y = 0;
            float maxHeightInRow = 0;
            const float padding = 50; // Increased padding between groups
            const float maxWidth = 2000; // Increased max width to allow more groups per row
            var i = 0;

            foreach (var kvp in sortedGroups) // For each group execute layout group
            {
                ++i;
                var group = kvp.Value;
                LayoutNodesInGroup(group, i); // Layout nodes before calculating group size
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
            }
        }

        private static void LayoutNodesInGroup(Group group, int groupNumber)
        {
            float x = 0;
            float y = groupNumber * 250; // Increased initial y to leave more space for group title
            float maxHeightInRow = 0;
            float padding = 20; // Increased padding between nodes
            float maxWidth = 300; // Fixed width for all groups

            foreach (var element in group.containedElements) // Iterating over nodes in group
            {
                if (element is not Node node) continue;
                node.SetPosition(new Rect(x, y, node.contentRect.width, node.contentRect.height));

                x += node.contentRect.width + padding;
                maxHeightInRow = Mathf.Max(maxHeightInRow, node.contentRect.height);
            }

            // Update group size to fit all nodes
            float groupWidth = Mathf.Max(maxWidth, 10 + (groupNumber-1) % 5 * 500); // Ensure minimum width
            float groupHeight = y + maxHeightInRow + padding;
            group.SetPosition(new Rect(group.contentRect.x, group.contentRect.y, groupWidth, groupHeight));
        }

        private void AddMiniMap()
        {
            var minimap = new MiniMap()
            {
                anchored = true
            };
            minimap.SetPosition(new Rect(15, 50, 200, 100));
            Add(minimap);
        }
    }
}