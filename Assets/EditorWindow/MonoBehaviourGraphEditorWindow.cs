using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace EditorWindow
{


    public class MonoBehaviourGraphWindow : UnityEditor.EditorWindow
    {
        private MonoBehaviourGraphView _graphView;
        private bool _includeInactiveObjects = true;
        private bool _includeBuiltInComponents = true;

        [MenuItem("Window/MonoBehaviour Graph")]
        public static void OpenWindow()
        {
            var window = GetWindow<MonoBehaviourGraphWindow>();
            window.titleContent = new GUIContent("MonoBehaviour Graph");
        }

        private void OnEnable()
        {
            ConstructGraphView();
        }

        private void ConstructGraphView()
        {
            _graphView = new MonoBehaviourGraphView(this)
            {
                name = "MonoBehaviour Graph",
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }
        
        private void OnGUI()
        {
            DrawToolbar();
        
            // This ensures the GraphView fills the rest of the window
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));
            //_graphView.SetPosition(rect);
        }


        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Generate Graph", EditorStyles.toolbarButton))
            {
                GenerateGraph();
            }

            _includeInactiveObjects = EditorGUILayout.ToggleLeft("Include Inactive", _includeInactiveObjects, GUILayout.Width(120));
            _includeBuiltInComponents = EditorGUILayout.ToggleLeft("Include Built-in", _includeBuiltInComponents, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();
        }

        private void GenerateGraph()
        {
            _graphView.PopulateView(_includeInactiveObjects, _includeBuiltInComponents);
        }
    }

    public class MonoBehaviourGraphView : GraphView
    {
        private readonly MonoBehaviourGraphWindow _window;

        public MonoBehaviourGraphView(MonoBehaviourGraphWindow window)
        {
            _window = window;

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var contentZoomer = new ContentZoomer();
            contentZoomer.minScale = 0.1f;
            contentZoomer.maxScale = 2f;
            this.AddManipulator(contentZoomer);

            var contentDragger = new ContentDragger();
            this.AddManipulator(contentDragger);
        }

        public void PopulateView(bool includeInactive, bool includeBuiltIn)
        {
            DeleteElements(graphElements.ToList());

            Component[] components = includeInactive
                ? Resources.FindObjectsOfTypeAll<Component>()
                : Object.FindObjectsOfType<Component>();

            var nodes = new Dictionary<Component, ComponentNode>();

            foreach (var component in components)
            {
                if (!includeBuiltIn && IsBuiltInComponent(component))
                    continue;

                var node = new ComponentNode(component);
                nodes[component] = node;
                AddElement(node);
            }

            foreach (var component in components)
            {
                if (!nodes.ContainsKey(component))
                    continue;

                var sourceNode = nodes[component];

                // Connect to other components on the same GameObject
                foreach (var otherComponent in component.gameObject.GetComponents<Component>())
                {
                    if (nodes.TryGetValue(otherComponent, out var targetNode) && otherComponent != component)
                    {
                        ConnectNodes(sourceNode, targetNode);
                    }
                }

                // Connect based on serialized fields
                var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (typeof(Component).IsAssignableFrom(field.FieldType))
                    {
                        var connectedComponent = field.GetValue(component) as Component;
                        if (connectedComponent && nodes.TryGetValue(connectedComponent, out var targetNode))
                        {
                            ConnectNodes(sourceNode, targetNode);
                        }
                    }
                }
            }
        }

        private void ConnectNodes(ComponentNode source, ComponentNode target)
        {
            var edge = source.OutputPort.ConnectTo(target.InputPort);
            AddElement(edge);
        }

        private bool IsBuiltInComponent(Component component)
        {
            var type = component.GetType();
            return type.Namespace?.StartsWith("UnityEngine") == true || type.Namespace?.StartsWith("UnityEditor") == true;
        }
    }

    public class ComponentNode : Node
    {
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        public ComponentNode(Component component)
        {
            title = component.GetType().Name;
            viewDataKey = component.GetInstanceID().ToString();

            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(Component));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Component));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);

            var gameObjectLabel = new Label($"GameObject: {component.gameObject.name}");
            mainContainer.Add(gameObjectLabel);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}