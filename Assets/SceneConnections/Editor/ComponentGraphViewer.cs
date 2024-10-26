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

        [MenuItem("Window/Connections v2 %#2")]
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
                if (_isRefreshing) return;
                _isRefreshing = true;
                EditorApplication.delayCall += () =>
                {
                    _graphView.RefreshGraph();
                    // Schedule a second layout pass after everything is initialized
                    EditorApplication.delayCall += () =>
                    {
                        _graphView.ForceLayoutRefresh();
                        _isRefreshing = false;
                    };
                };
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
        private bool _needsLayout;
        private readonly Label _loadingLabel;


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
            // Add to existing constructor
            _loadingLabel = new Label("Calculating layout...")
            {
                style =
                {
                    display = DisplayStyle.None,
                    position = Position.Absolute,
                    top = 10,
                    left = 10,
                    backgroundColor = new Color(.5f, 0, 0, 0.8f),
                    color = Color.white
                }
            };
            Add(_loadingLabel);
        }

        public void RefreshGraph()
        {
            ClearGraph();
            CreateComponentGraph();
            _loadingLabel.style.display = DisplayStyle.Flex;
            _needsLayout = true;
            EditorApplication.delayCall += PerformLayout;
        }

        private void PerformLayout()
        {
            if (!_needsLayout) return;

            LayoutNodes();
            _loadingLabel.style.display = DisplayStyle.None;
            _needsLayout = false;
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
                var fields = sourceComponent.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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

        private static Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
        }


        private void LayoutNodes()
        {
            float x = 0;
            float y = 0;
            float maxHeightInRow = 0;
            const float groupPadding = 50;
            const float maxWidth = 2000;

            foreach (var kvp in _gameObjectGroups)
            {
                var group = kvp.Value;
                LayoutNodesInGroup(group);
                group.UpdateGeometryFromContent();

                // Check if the group exceeds the row width
                if (x + group.contentRect.width > maxWidth)
                {
                    x = 0;
                    y += maxHeightInRow + groupPadding;
                    maxHeightInRow = 0;
                }

                group.SetPosition(new Rect(x, y, group.contentRect.width, group.contentRect.height));

                x += group.contentRect.width + groupPadding;
                maxHeightInRow = Mathf.Max(maxHeightInRow, group.contentRect.height);
            }
        }

        private static void LayoutNodesInGroup(Group group)
        {
            const float nodePadding = 20;
            const float maxGroupWidth = 800;
            var currentX = nodePadding;
            float currentY = 50; // Space for group title

            var nodes = group.containedElements.OfType<Node>().ToList();
            float maxHeightInRow = 0;
            float rowWidth = 0;

            // Debug log the number of nodes
            Debug.Log($"Laying out {nodes.Count} nodes in group {group.title}");

            foreach (var node in nodes)
            {
                // Debug log each node's dimensions
                Debug.Log($"Node {node.title} - Width: {node.contentRect.width}, Height: {node.contentRect.height}");

                // Use a minimum width if contentRect is not yet calculated
                var nodeWidth = Mathf.Max(node.contentRect.width, 200f);
                var nodeHeight = Mathf.Max(node.contentRect.height, 100f);

                // Check if we need to move to the next row
                if (currentX + nodeWidth > maxGroupWidth)
                {
                    // Debug log row wrap
                    Debug.Log($"Moving to next row at y: {currentY}");

                    currentX = nodePadding;
                    currentY += maxHeightInRow + nodePadding;
                    maxHeightInRow = 0;
                }

                // Debug log node position
                Debug.Log($"Positioning node {node.title} at X: {currentX}, Y: {currentY}");

                // Set the position
                var newRect = new Rect(currentX, currentY, nodeWidth, nodeHeight);
                node.SetPosition(newRect);

                // Update tracking variables
                currentX += nodeWidth + nodePadding;
                maxHeightInRow = Mathf.Max(maxHeightInRow, nodeHeight);
                rowWidth = Mathf.Max(rowWidth, currentX);
            }

            // Update group size
            var finalHeight = currentY + maxHeightInRow + nodePadding;
            var finalWidth = Mathf.Min(maxGroupWidth, rowWidth + nodePadding);

            // Debug log final group dimensions
            Debug.Log($"Setting group {group.title} dimensions - Width: {finalWidth}, Height: {finalHeight}");

            group.SetPosition(new Rect(
                group.contentRect.x,
                group.contentRect.y,
                finalWidth,
                finalHeight
            ));
        }

// Add this helper method to force a layout refresh
        public void ForceLayoutRefresh()
        {
            foreach (var node in _gameObjectGroups.Values.SelectMany(group => group.containedElements.OfType<Node>()))
            {
                // Force the node to calculate its layout
                node.RefreshExpandedState();
                node.RefreshPorts();
            }

            // Schedule the layout for the next frame
            EditorApplication.delayCall += () =>
            {
                LayoutNodes();
                // Force the graph view to update
                UpdateViewTransform(viewTransform.position, viewTransform.scale);
            };
        }


        private void AddMiniMap()
        {
            var minimap = new NavigableMinimap(this);
            minimap.SetPosition(new Rect(15, 50, 200, 100));
            Add(minimap);
        }
    }
}