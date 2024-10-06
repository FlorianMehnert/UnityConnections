using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.UIElements;

public class ClassOverviewGraph : UnityEditor.EditorWindow
{
    private ClassOverviewGraphView graphView;

    [MenuItem("Window/Class Overview Graph")]
    public static void OpenWindow()
    {
        GetWindow<ClassOverviewGraph>("Class Overview Graph");
    }

    private void OnEnable()
    {
        graphView = new ClassOverviewGraphView(this);
        graphView.StretchToParentSize();
        rootVisualElement.Add(graphView);
    }

    private void OnDisable()
    {
        rootVisualElement.Remove(graphView);
    }

    private void OnGUI()
    {
        // Add a refresh button
        if (GUILayout.Button("Refresh Graph"))
        {
            graphView.RefreshGraph();
        }
    }
}

public class ClassOverviewGraphView : GraphView
{
    private ClassOverviewGraph editorWindow;
    private Vector2 graphOffset = new Vector2(200, 200);

    public ClassOverviewGraphView(ClassOverviewGraph editorWindow)
    {
        this.editorWindow = editorWindow;
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // Set the viewTransform to move the content into view
        this.viewTransform.position = graphOffset;

        CreateClassGraph();
    }

    public void RefreshGraph()
    {
        DeleteElements(graphElements.ToList());
        CreateClassGraph();
    }

    private void CreateClassGraph()
    {
        var allTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(MonoBehaviour).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        Debug.Log($"Found {allTypes.Count} MonoBehaviour types");

        var typeGroups = allTypes.GroupBy(type => type.Namespace);
        var classNodes = new Dictionary<Type, Node>();

        Vector2 position = Vector2.zero;
        foreach (var group in typeGroups)
        {
            var container = CreateGroupContainer(group.Key ?? "Global");
            container.SetPosition(new Rect(position, new Vector2(300, 300)));

            foreach (var type in group)
            {
                var node = CreateClassNode(type);
                classNodes[type] = node;
                container.AddElement(node);
            }

            AddElement(container);
            position += new Vector2(350, 0);
        }

        CreateConnections(classNodes);

        // Force the graph to update its view
        UpdateViewTransform(viewTransform.position, viewTransform.scale);
    }

    private Node CreateClassNode(Type type)
    {
        var node = new Node
        {
            title = type.Name,
            expanded = true
        };

        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        foreach (var field in fields)
        {
            node.AddPort(field.Name, field.FieldType);
        }

        node.SetPosition(new Rect(UnityEngine.Random.Range(10, 290), UnityEngine.Random.Range(10, 290), 100, 150));
        return node;
    }

    private Group CreateGroupContainer(string title)
    {
        return new Group
        {
            title = title
        };
    }

    private void CreateConnections(Dictionary<Type, Node> classNodes)
    {
        foreach (var kvp in classNodes)
        {
            var type = kvp.Key;
            var node = kvp.Value;

            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (classNodes.TryGetValue(field.FieldType, out var targetNode))
                {
                    var edge = node.Q<Port>(field.Name).ConnectTo<Edge>(targetNode.inputContainer.Q<Port>());
                    AddElement(edge);
                }
            }
        }
    }
}

public static class NodeExtensions
{
    public static Port AddPort(this Node node, string name, Type type)
    {
        var port = node.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, type);
        port.portName = name;
        node.outputContainer.Add(port);
        node.RefreshPorts();
        node.RefreshExpandedState();
        return port;
    }
}