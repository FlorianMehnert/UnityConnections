using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.UIElements;
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

        /// <summary>
        /// register all buttons etc
        /// </summary>
        private void OnEnable()
        {
            _graphView = new ComponentGraphView(this);
            rootVisualElement.Add(_graphView);

            var toolbar = new Toolbar();

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
            toolbar.Add(refreshButton);

            var applyForceLayoutButton = new Button(() => { _graphView.ApplyForceDirectedLayout(); }) { text = "Apply Force Layout" };
            var groupPaddingSlider = new Slider("Group Padding", 20, 100, SliderDirection.Horizontal);
            groupPaddingSlider.value = _graphView._groupPadding;
            groupPaddingSlider.RegisterValueChangedCallback(evt => 
            {
                _graphView._groupPadding = evt.newValue;
                _graphView.ApplyForceDirectedLayout();
            });
            toolbar.Add(groupPaddingSlider);
            toolbar.Add(applyForceLayoutButton);

            rootVisualElement.Add(toolbar);
        }


        private class ComponentGraphView : GraphView
        {
            private readonly Vector2 _forceStrength = new Vector2(50f, 50f);
            private float _repulsionForce = 500f;
            private float _springLength = 100f;
            private float _damping = 0.8f;
            private Dictionary<Node, Vector2> _nodeVelocities = new Dictionary<Node, Vector2>();
            private Dictionary<Component, Node> _componentNodes = new Dictionary<Component, Node>();
            private Dictionary<GameObject, Group> _gameObjectGroups = new Dictionary<GameObject, Group>();
            private Dictionary<Group, Vector2> _groupVelocities = new Dictionary<Group, Vector2>();
            public float _groupPadding = 50f;

            // Add user controls
            
            
            private void AddUserControls(ComponentGraphViewer window)
            {
                var controlsContainer = new VisualElement();
                controlsContainer.style.flexDirection = FlexDirection.Column;
                controlsContainer.style.position = Position.Absolute;
                controlsContainer.style.top = 10;
                controlsContainer.style.left = 10;

                var spacingSlider = new Slider("Node Spacing", 50, 200);
                spacingSlider.value = _springLength;
                spacingSlider.RegisterValueChangedCallback(evt =>
                {
                    _springLength = evt.newValue;
                    ApplyForceDirectedLayout();
                });

                var repulsionSlider = new Slider("Repulsion Force", 100, 1000);
                repulsionSlider.value = _repulsionForce;
                repulsionSlider.RegisterValueChangedCallback(evt =>
                {
                    _repulsionForce = evt.newValue;
                    ApplyForceDirectedLayout();
                });

                controlsContainer.Add(spacingSlider);
                controlsContainer.Add(repulsionSlider);

                window.rootVisualElement.Add(controlsContainer);
            }

            public ComponentGraphView(ComponentGraphViewer window)
            {
                SetupZoom(.01f, 5.0f);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());
                AddMiniMap();
                AddUserControls(window);

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
                var sortedGroups = _gameObjectGroups.OrderBy(kvp => kvp.Key.transform.GetHierarchyDepth());

                float x = 0;
                float y = 0;
                float maxHeightInRow = 0;
                float padding = 50; // Increased padding between groups
                float maxWidth = 2000; // Increased max width to allow more groups per row

                foreach (var kvp in sortedGroups)
                {
                    var group = kvp.Value;
                    LayoutNodesInGroup(group); // Layout nodes before calculating group size
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

            private void AddMiniMap()
            {
                MiniMap minimap = new MiniMap()
                {
                    anchored = true
                };
                minimap.SetPosition(new Rect(15, 50, 200, 100));
                Add(minimap);
            }

            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                base.BuildContextualMenu(evt);
                evt.menu.AppendAction("Apply Force-Directed Layout", _ => ApplyForceDirectedLayout());
            }

            public void ApplyForceDirectedLayout()
            {
                // Calculate sizes for each group
                foreach (var group in _gameObjectGroups.Values)
                {
                    CalculateGroupSize(group);
                }

                // Initialize velocities
                _groupVelocities.Clear();
                foreach (var group in _gameObjectGroups.Values)
                {
                    _groupVelocities[group] = Vector2.zero;
                }

                // Run simulation
                for (int i = 0; i < 100; i++) // Adjust iteration count as needed
                {
                    ApplyForces();
                    UpdatePositions();
                }

                // Update node positions within groups
                foreach (var group in _gameObjectGroups.Values)
                {
                    LayoutNodesInGroup(group);
                }
            }
            
            private void CalculateGroupSize(Group group)
            {
                if (!group.containedElements.Any()) return;

                float totalArea = group.containedElements.OfType<Node>().Sum(node => node.contentRect.width * node.contentRect.height);
                float aspectRatio = 1.5f; // Adjust this to change the shape of the groups
                float width = Mathf.Sqrt(totalArea * aspectRatio) + _groupPadding * 2;
                float height = width / aspectRatio;

                group.SetPosition(new Rect(group.contentRect.x, group.contentRect.y, width, height));
            }

            private void ApplyForces()
            {
                var groups = _gameObjectGroups.Values.ToList();

                for (int i = 0; i < groups.Count; i++)
                {
                    var groupA = groups[i];
                    var posA = groupA.GetPosition().center;

                    // Repulsion (between all groups)
                    for (int j = i + 1; j < groups.Count; j++)
                    {
                        var groupB = groups[j];
                        var posB = groupB.GetPosition().center;
                        var direction = (posA - posB).normalized;
                        var distance = Vector2.Distance(posA, posB);
                        var force = direction * _repulsionForce / (distance + 1);

                        _groupVelocities[groupA] += force;
                        _groupVelocities[groupB] -= force;
                    }

                    // Attraction (between connected groups)
                    foreach (var edge in edges)
                    {
                        var sourceGroup = (edge.output.node as Node)?.parent as Group;
                        var targetGroup = (edge.input.node as Node)?.parent as Group;

                        if (sourceGroup == groupA && targetGroup != null && sourceGroup != targetGroup)
                        {
                            var posB = targetGroup.GetPosition().center;
                            var direction = (posB - posA).normalized;
                            var distance = Vector2.Distance(posA, posB);
                            var force = direction * (distance - _springLength) * _forceStrength;

                            _groupVelocities[groupA] += force;
                            _groupVelocities[targetGroup] -= force;
                        }
                    }
                }
            }

            private void UpdatePositions()
            {
                foreach (var group in _gameObjectGroups.Values)
                {
                    var pos = group.GetPosition();
                    _groupVelocities[group] *= _damping;
                    pos.position += _groupVelocities[group] * 0.1f; // Adjust multiplier to control speed
                    group.SetPosition(pos);
                }
            }
            
            private void LayoutNodesInGroup(Group group)
            {
                var nodes = group.containedElements.OfType<Node>().ToList();
                if (nodes.Count == 0) return;

                var groupRect = group.contentRect;
                float availableWidth = groupRect.width - _groupPadding * 2;
                float availableHeight = groupRect.height - _groupPadding * 2;

                int cols = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count * availableWidth / availableHeight));
                int rows = Mathf.CeilToInt((float)nodes.Count / cols);

                float nodeWidth = availableWidth / cols;
                float nodeHeight = availableHeight / rows;

                for (int i = 0; i < nodes.Count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;

                    float x = groupRect.x + _groupPadding + col * nodeWidth;
                    float y = groupRect.y + _groupPadding + row * nodeHeight;

                    nodes[i].SetPosition(new Rect(x, y, nodeWidth - 5, nodeHeight - 5));
                }
            }

            private void UpdateGroupPosition(Group group)
            {
                if (!group.containedElements.Any()) return;

                var groupElements = group.containedElements.OfType<GraphElement>().ToList();
                var minX = groupElements.Min(e => e.GetPosition().xMin);
                var minY = groupElements.Min(e => e.GetPosition().yMin);
                var maxX = groupElements.Max(e => e.GetPosition().xMax);
                var maxY = groupElements.Max(e => e.GetPosition().yMax);

                var padding = 20;
                group.SetPosition(new Rect(minX - padding, minY - padding,
                    maxX - minX + padding * 2, maxY - minY + padding * 2));
            }
        }
    }
}