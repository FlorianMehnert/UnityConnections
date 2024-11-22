using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SceneConnections.Editor.Nodes;
using SceneConnections.Editor.Utils;
using SceneConnections.Editor.Utils.ScriptVisualization;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Edge = UnityEditor.Experimental.GraphView.Edge;
using Object = UnityEngine.Object;


namespace SceneConnections.Editor
{
    public class ComponentGraphView : GraphView, IConnectionGraphView
    {
        /// <summary>
        /// Store all Node ↔ Component relationships for the cases of <b>NodesAreComponents</b>
        /// </summary>
        private readonly Dictionary<Component, Node> _componentNodes = new();

        private readonly Dictionary<GameObject, Group> _gameObjectGroups = new();
        private readonly Dictionary<string, Node> _scripts = new();

        private int _currentDebuggedRect;
        private bool _needsLayout;

        private IConnectionGraphView _connectionGraphViewImplementation;


        public ComponentGraphView(Color defaultNodeColor, Color highlightColor)
        {
            Nodes = new List<Node>();
            HighlightColor = highlightColor;
            DefaultNodeColor = defaultNodeColor;
            SetupZoom(.01f, 5.0f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            var gridBackground = new GridBackground();
            Insert(0, gridBackground);
            gridBackground.StretchToParentSize();
            style.flexGrow = 1;
            style.flexShrink = 1;
            RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            NodeGraphBuilder = new NodeGraphBuilder(this);
            NodeGraphBuilder.SetupProgressBar();
            var interfaceBuilder = new InterfaceBuilder(this);
            interfaceBuilder.SetupUI();
            PathTextFieldValue = "/home/florian/gamedev/my-own-platformer/Assets/SceneConnections/";
            GraphDrawType = Constants.ComponentGraphDrawType.NodesAreScripts;
            ReferenceInheritance = true;
            ReferenceFields = true;
            ReferenceMethods = true;
        }

        /// <summary>
        /// Handle KeyDown presses - shortcut handling
        /// </summary>
        /// <param name="evt"></param>
        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            switch (evt.ctrlKey)
            {
                case true when evt.keyCode == KeyCode.R:
                    RefreshGraph();
                    evt.StopPropagation();
                    break;
                case true when evt.keyCode == KeyCode.L:
                    NodeLayoutManager.DisableDisconnectedNodes(Nodes, edges.ToList());
                    break;
                case true when evt.keyCode == KeyCode.C:
                    NodeGraphBuilder.BuildGraph();
                    evt.StopPropagation();
                    break;
                case true when evt.keyCode == KeyCode.I:
                {
                    NodeLayoutManager.PhysicsBasedLayoutParallel(Nodes, edges.ToList());
                    evt.StopPropagation();
                    break;
                }
                case true when evt.keyCode == KeyCode.T:
                {
                    switch (GraphDrawType)
                    {
                        case Constants.ComponentGraphDrawType.NodesAreComponents:
                        {
                            var groups = _gameObjectGroups.Values.ToArray();
                            var layoutState = NodeUtils.OptimizeGroupLayouts(groups, padding: 15f);
                            layoutState.ApplyLayout();
                            break;
                        }
                        case Constants.ComponentGraphDrawType.NodesAreGameObjects:
                            UpdateLayout();
                            break;
                        case Constants.ComponentGraphDrawType.NodesAreScripts:
                            UpdateLayout();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    evt.StopPropagation();
                    break;
                }
                case true when evt.keyCode == KeyCode.X:
                {
                    GraphViewUtilities.ExportGraphToGraphviz(this, "Assets/UnityGraph.dot");
                    Debug.Log("Graph exported to Graphviz format at Assets/UnityGraph.dot");
                    evt.StopPropagation();
                    break;
                }
            }
        }

        /// <summary>
        /// Destroy current nodes and recreate everything
        /// </summary>
        public void RefreshGraph()
        {
            ClearGraph();
            CreateGraph(GraphDrawType);
        }

        private void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
            _componentNodes.Clear();
            _gameObjectGroups.Clear();
            Nodes.Clear();
        }

        /// <summary>
        /// Creating node overview using all GameObjects
        /// </summary>
        /// <param name="representation">ComponentGraphDrawType deciding whether nodes are game objects or nodes are components grouped using groups</param>
        private void CreateGraph(
            Constants.ComponentGraphDrawType representation = Constants.ComponentGraphDrawType.NodesAreComponents)
        {
            var allGameObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
            Nodes = new List<Node>();
            switch (representation)
            {
                // groups contain nodes that are components of a game object
                case Constants.ComponentGraphDrawType.NodesAreComponents:
                {
                    foreach (var gameObject in allGameObjects)
                    {
                        CreateGameObjectGroup(gameObject);

                        var components = gameObject.GetComponents<Component>();
                        foreach (var component in components)
                        {
                            CreateComponentNode(component);
                        }
                    }
                    break;
                }
                // nodes contain attributes that correspond to attached components
                case Constants.ComponentGraphDrawType.NodesAreGameObjects:
                {
                    foreach (var gameObject in allGameObjects)
                    {
                        var components = gameObject.GetComponents<Component>();
                        Nodes.Add(CreateCompactNode(gameObject, components));
                    }

                    break;
                }
                case Constants.ComponentGraphDrawType.NodesAreScripts:
                {
                    // 0. create dict that stores scripts and their corresponding references
                    // 1. collect scripts
                    // 2. parse scripts -> add references in nodes
                    // 3. add node for each script with references
                    // 4. update layout
                    // 5. group if needed

                    Dictionary<string, ClassReferences> allReferences;
                    if (PathTextFieldValue != "")
                    {
                        allReferences = ClassParser.GetAllClassReferencesParallel(PathTextFieldValue, ReferenceInheritance, ReferenceFields, ReferenceMethods);
                    }
                    else
                    {
                        var scriptPaths = ScriptFinder.GetAllScriptPaths();
                        allReferences = ClassParser.GetAllClassReferencesParallel(scriptPaths);
                    }


                    foreach (var (scriptName, _) in allReferences)
                    {
                        var node = new AdvancedNode { title = scriptName };
                        _scripts[scriptName] = node;
                        Nodes.Add(node);
                        AddElement(node); // disable for faster export
                    }

                    Parallel.ForEach(allReferences, reference =>
                    {
                        var sourceScriptName = reference.Key;
                        if (!_scripts.TryGetValue(sourceScriptName, out var sourceNode)) return;

                        CreateReference(reference, sourceNode);
                    });
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(representation), representation, null);
            }

            CreateEdges();
            LayoutNodes(representation);
        }

        /// <summary>
        /// Helper for <see cref="CreateGraph"/>
        /// </summary>
        /// <param name="reference">key value pair containing the current string, ClassReference combination</param>
        /// <param name="sourceNode">Node to which the edges will be connected to</param>
        private void CreateReference(KeyValuePair<string, ClassReferences> reference, Node sourceNode)
        {
            foreach (var className in reference.Value.References.Select(referencedScript =>
                         referencedScript.Contains(".")
                             ? referencedScript[
                                 (referencedScript.LastIndexOf(".", StringComparison.Ordinal) + 1)..]
                             : referencedScript))
            {
                if (_scripts.TryGetValue(className, out var targetNode))
                {
                    // Use dispatcher to create edge on main thread since Unity UI must be modified on main thread
                    EditorApplication.delayCall += () => CreateEdge(sourceNode, targetNode);
                }
            }
        }

        private void UpdateLayout()
        {
            LayoutNodesUsingManager();
            foreach (var node in nodes)
            {
                node.RefreshExpandedState();
                node.RefreshPorts();
            }
        }


        /// <summary>
        /// Create Group object for passed gameObject for the cases of <b>NodesAreComponents</b>
        /// </summary>
        /// <param name="gameObject"></param>
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

        /// <summary>
        /// Create Nodes for components in the case of <b>NodesAreComponents</b> within the corresponding group
        /// </summary>
        /// <param name="component"></param>
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
            Nodes.Add(node);

            if (_gameObjectGroups.TryGetValue(component.gameObject, out var group))
            {
                group.AddElement(node);
            }
        }

        /// <summary>
        /// Create a node with the name of the gameObject and all its components in cases of <b>NodesAreGameObjects</b>
        /// </summary>
        /// <param name="gameObject">GameObject after which the node will be named after</param>
        /// <param name="components">All the connected components of the gameObjectNode</param>
        /// <returns></returns>
        private Node CreateCompactNode(GameObject gameObject, Component[] components)
        {
            var node = new Node
            {
                title = gameObject.name
            };

            // Add component list
            foreach (var component in components)
            {
                var componentField = new ObjectField(component.GetType().Name)
                {
                    objectType = component.GetType(),
                    value = component,
                    allowSceneObjects = true
                };
                node.mainContainer.Add(componentField);
            }

            // Add node to graph
            AddElement(node);
            return node;
        }

        /// <summary>
        /// Apply Layout using the LayoutManager
        /// </summary>
        private void LayoutNodesUsingManager()
        {
            NodeLayoutManager.LayoutNodes(Nodes);
        }

        /// <summary>
        /// In case of <b>NodesAreComponents</b> call this to visualize the attributes of a component
        /// </summary>
        /// <param name="node">Node corresponding to the component</param>
        /// <param name="component">Component of which the parameters should be added</param>
        /// <param name="maximumParameters">maximal amount of Parameters that should be added to the node to avoid visual clutter</param>
        private static void AddComponentProperties(Node node, Component component, int maximumParameters = 5)
        {
            var properties = component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && !p.GetIndexParameters().Any());

            foreach (var property in properties.Take(maximumParameters))
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

        /// <summary>
        /// In case of NodesAreComponents Generate Edges between components
        /// </summary>
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

        /// <summary>
        /// Helper method for <see cref="CreateEdges"/> Generating a link between <i>sourceNode</i> and <i>targetNode</i>
        /// </summary>
        /// <param name="sourceNode">Node that is origin of the connection</param>
        /// <param name="targetNode">Node that is target of the connection</param>
        private void CreateEdge(Node sourceNode, Node targetNode)
        {
            if (sourceNode == targetNode) return; // Prevent self-referencing edges

            // Check if edge already exists
            if (edges.Any(e =>
                    (e.output.node == sourceNode && e.input.node == targetNode) ||
                    (e.input.node == sourceNode && e.output.node == targetNode)))
            {
                return;
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
            AddElement(edge);
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

        /// <summary>
        /// Wrapper for <see cref="Node.InstantiatePort"/>
        /// </summary>
        /// <param name="node">Node receiving the port</param>
        /// <param name="direction">direction in which the port will be added</param>
        /// <param name="capacity">amount of connections allowed per port</param>
        /// <returns></returns>
        private static Port GeneratePort(Node node, Direction direction, Port.Capacity capacity)
        {
            return node.InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(Component));
        }

        /// <summary>
        /// Call to organize the layout of all nodes for <b>NodesAreComponents</b>
        /// </summary>
        private void LayoutNodes(Constants.ComponentGraphDrawType representation)
        {
            switch (representation)
            {
                case Constants.ComponentGraphDrawType.NodesAreComponents:
                {
                    float x = 0;
                    float y = 0;
                    float maxHeightInRow = 0;
                    const float groupPadding = 50;
                    const float maxWidth = 5000;

                    foreach (var group in _gameObjectGroups.Select(kvp => kvp.Value))
                    {
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

                    break;
                }
                case Constants.ComponentGraphDrawType.NodesAreGameObjects:
                    UpdateLayout();
                    break;
                case Constants.ComponentGraphDrawType.NodesAreScripts:
                    UpdateLayout();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(representation), representation, null);
            }
        }

        /// <summary>
        /// In case of <b>NodesAreComponents</b> layout nodes within a group
        /// </summary>
        /// <param name="group">a group can contain nodes</param>
        private static void LayoutNodesInGroup(Group group)
        {
            const float nodePadding = 20;
            const float maxGroupWidth = 800;
            var currentX = nodePadding;
            float currentY = 50; // Space for group title

            var nodes = group.containedElements.OfType<Node>().ToList();
            float maxHeightInRow = 0;
            float rowWidth = 0;

            foreach (var node in nodes)
            {
                // Use a minimum width if contentRect is not yet calculated
                var nodeWidth = Mathf.Max(node.contentRect.width, 50f);
                var nodeHeight = Mathf.Max(node.contentRect.height, 100f);

                // Check if we need to move to the next row
                if (currentX + nodeWidth > maxGroupWidth)
                {
                    currentX = nodePadding;
                    currentY += maxHeightInRow + nodePadding;
                    maxHeightInRow = 0;
                }

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

            group.SetPosition(new Rect(
                group.contentRect.x,
                group.contentRect.y,
                finalWidth,
                finalHeight
            ));
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
        
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                {
                    compatiblePorts.Add(port);
                }
            });
        
            return compatiblePorts;
        }


        public Constants.ComponentGraphDrawType GraphDrawType { get; set; }

        public string PathTextFieldValue
        {
            get => PathTextField.value;
            set => PathTextField.value = value;
        }

        public TextField PathTextField { get; set; }

        public TextField SearchField { get; set; }
        public StyleColor HighlightColor { get; }
        public List<Node> Nodes { get; private set; }
        public StyleColor DefaultNodeColor { get; }

        public bool IsBusy { get; set; }

        public List<GraphElement> GraphElements => graphElements.ToList();
        public NodeGraphBuilder NodeGraphBuilder { get; }

        public bool ReferenceInheritance { get; set; }
        public bool ReferenceFields { get; set; }
        public bool ReferenceMethods { get; set; }
    }
}