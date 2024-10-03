using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

public class MonoBehaviourGraphWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private float zoomLevel = 1f;
    private Vector2 graphOffset;
    private Dictionary<Component, NodeInfo> nodeInfos = new Dictionary<Component, NodeInfo>();
    private Component selectedNode;
    private bool includeInactiveObjects = true;
    private bool includeBuiltInComponents = true;

    private class NodeInfo
    {
        public Rect Position;
        public List<Component> Inputs = new List<Component>();
        public List<Component> Outputs = new List<Component>();
        public bool IsActive;
        public bool IsBuiltIn;
    }

    [MenuItem("Window/MonoBehaviour Graph")]
    public static void ShowWindow()
    {
        GetWindow<MonoBehaviourGraphWindow>("MonoBehaviour Graph");
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Graph"))
        {
            GenerateGraph();
        }
        includeInactiveObjects = EditorGUILayout.ToggleLeft("Include Inactive", includeInactiveObjects);
        includeBuiltInComponents = EditorGUILayout.ToggleLeft("Include Built-in", includeBuiltInComponents);
        EditorGUILayout.EndHorizontal();

        HandleEvents();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawGraph();
        EditorGUILayout.EndScrollView();

        if (selectedNode != null)
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Selected: " + selectedNode.GetType().Name, EditorStyles.boldLabel);
            Editor editor = Editor.CreateEditor(selectedNode);
            editor.OnInspectorGUI();
        }
    }

    private void GenerateGraph()
    {
        nodeInfos.Clear();
        Component[] allComponents = includeInactiveObjects 
            ? Resources.FindObjectsOfTypeAll<Component>()
            : FindObjectsOfType<Component>();

        // First pass: create NodeInfo for each Component
        foreach (Component component in allComponents)
        {
            bool isBuiltIn = IsBuiltInComponent(component);
            if (includeBuiltInComponents || !isBuiltIn)
            {
                nodeInfos[component] = new NodeInfo
                {
                    IsActive = component.gameObject.activeInHierarchy && (!(component is Behaviour) || ((Behaviour)component).enabled),
                    IsBuiltIn = isBuiltIn
                };
            }
        }

        // Second pass: analyze connections
        foreach (Component component in nodeInfos.Keys.ToList())
        {
            AnalyzeConnections(component);
        }

        // Third pass: position nodes
        PositionNodes();
    }

    private bool IsBuiltInComponent(Component component)
    {
        System.Type componentType = component.GetType();
        string namespaceName = componentType.Namespace;

        // Check if the component is from Unity's built-in namespaces
        if (!string.IsNullOrEmpty(namespaceName) && 
            (namespaceName.StartsWith("UnityEngine") || namespaceName.StartsWith("UnityEditor")))
        {
            return true;
        }

        // Check if the component is a built-in Unity component without a namespace
        string[] builtInComponents = new string[]
        {
            "Transform", "RectTransform", "Rigidbody", "Rigidbody2D", "Collider", "Collider2D",
            "MeshRenderer", "SkinnedMeshRenderer", "Camera", "Light", "AudioSource", "AudioListener",
            "Animator", "Animation", "Canvas", "CanvasRenderer", "GraphicRaycaster", "ParticleSystem"
            // Add more built-in component names as needed
        };

        return builtInComponents.Contains(componentType.Name);
    }

    private void AnalyzeConnections(Component component)
    {
        FieldInfo[] fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (FieldInfo field in fields)
        {
            if (typeof(Component).IsAssignableFrom(field.FieldType))
            {
                Component connectedComponent = field.GetValue(component) as Component;
                if (connectedComponent != null && nodeInfos.ContainsKey(connectedComponent))
                {
                    nodeInfos[component].Outputs.Add(connectedComponent);
                    nodeInfos[connectedComponent].Inputs.Add(component);
                }
            }
        }

        // Check for connections through GameObject
        foreach (Component otherComponent in component.gameObject.GetComponents<Component>())
        {
            if (otherComponent != component && nodeInfos.ContainsKey(otherComponent))
            {
                nodeInfos[component].Outputs.Add(otherComponent);
                nodeInfos[otherComponent].Inputs.Add(component);
            }
        }
    }

    private void PositionNodes()
    {
        float x = 0;
        float y = 0;
        float maxHeight = 0;

        foreach (var kvp in nodeInfos)
        {
            Vector2 nodeSize = CalculateNodeSize(kvp.Key);
            if (x + nodeSize.x > position.width)
            {
                x = 0;
                y += maxHeight + 40;
                maxHeight = 0;
            }

            kvp.Value.Position = new Rect(x, y, nodeSize.x, nodeSize.y);
            x += nodeSize.x + 40;
            maxHeight = Mathf.Max(maxHeight, nodeSize.y);
        }
    }

    private void DrawGraph()
    {
        if (Event.current.type == EventType.Repaint)
        {
            DrawConnections();
            foreach (var kvp in nodeInfos)
            {
                DrawNode(kvp.Key, kvp.Value);
            }
        }
    }

    private void DrawNode(Component component, NodeInfo info)
    {
        Rect scaledRect = ScaleRect(info.Position);
        
        // Set color based on component type and active state
        if (info.IsBuiltIn)
            GUI.color = info.IsActive ? new Color(0.8f, 0.9f, 1f) : new Color(0.6f, 0.7f, 0.8f);
        else
            GUI.color = info.IsActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
        
        GUI.Box(scaledRect, "");
        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.UpperCenter;
        style.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(scaledRect.x, scaledRect.y + 5, scaledRect.width, 20), component.GetType().Name, style);

        style.fontStyle = FontStyle.Normal;
        style.fontSize = 10;
        GUI.Label(new Rect(scaledRect.x, scaledRect.y + 25, scaledRect.width, 20), $"In: {info.Inputs.Count}, Out: {info.Outputs.Count}", style);
        GUI.Label(new Rect(scaledRect.x, scaledRect.y + 40, scaledRect.width, 20), component.gameObject.name, style);

        if (scaledRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                selectedNode = component;
                Repaint();
            }
        }
    }

    private void DrawConnections()
    {
        foreach (var kvp in nodeInfos)
        {
            Component component = kvp.Key;
            NodeInfo info = kvp.Value;
            Rect startRect = ScaleRect(info.Position);

            foreach (Component connectedComponent in info.Outputs)
            {
                if (nodeInfos.TryGetValue(connectedComponent, out NodeInfo connectedInfo))
                {
                    Rect endRect = ScaleRect(connectedInfo.Position);
                    DrawConnectionLine(startRect.center, endRect.center, 
                        connectedComponent.gameObject == component.gameObject ? Color.green : Color.blue);
                }
            }
        }
    }

    private void DrawConnectionLine(Vector2 start, Vector2 end, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawBezier(start, end, start + Vector2.right * 50, end - Vector2.right * 50, color, null, 2f);
        Handles.EndGUI();
    }

    private Vector2 CalculateNodeSize(Component component)
    {
        float width = Mathf.Max(GUI.skin.box.CalcSize(new GUIContent(component.GetType().Name)).x + 20, 120);
        float height = 75;
        return new Vector2(width, height);
    }

    private void HandleEvents()
    {
        if (Event.current.type == EventType.ScrollWheel)
        {
            zoomLevel = Mathf.Clamp(zoomLevel - Event.current.delta.y * 0.01f, 0.1f, 2f);
            Event.current.Use();
            Repaint();
        }

        if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
        {
            graphOffset += Event.current.delta;
            Event.current.Use();
            Repaint();
        }
    }

    private Rect ScaleRect(Rect original)
    {
        return new Rect(
            (original.x + graphOffset.x) * zoomLevel,
            (original.y + graphOffset.y) * zoomLevel,
            original.width * zoomLevel,
            original.height * zoomLevel
        );
    }
}