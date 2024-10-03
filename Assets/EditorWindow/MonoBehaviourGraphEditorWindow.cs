using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace EditorWindow
{
    public class MonoBehaviourGraphWindow : UnityEditor.EditorWindow
    {
        private Vector2 _scrollPosition;
        private float _zoomLevel = 1f;
        private Vector2 _graphOffset;
        private readonly Dictionary<Component, NodeInfo> _nodeInfos = new();
        private readonly Dictionary<System.Type, GroupInfo> _groupInfos = new();
        private Component _selectedNode;
        private bool _includeInactiveObjects = true;
        private bool _includeBuiltInComponents = true;
        private bool _showGraph = true;

        private class NodeInfo
        {
            public Rect Position;
            public readonly List<Component> Inputs = new();
            public readonly List<Component> Outputs = new();
            public bool IsActive;
            public bool IsBuiltIn;
        }

        private class GroupInfo
        {
            public Rect Position;
            public List<Component> Components = new();
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
            _includeInactiveObjects = EditorGUILayout.ToggleLeft("Include Inactive", _includeInactiveObjects);
            _includeBuiltInComponents = EditorGUILayout.ToggleLeft("Include Built-in", _includeBuiltInComponents);
            _showGraph = EditorGUILayout.ToggleLeft("Showoff Graph", _showGraph);
            EditorGUILayout.EndHorizontal();

            HandleEvents();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawGraph();
            EditorGUILayout.EndScrollView();

            if (_selectedNode)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Selected: " + _selectedNode.GetType().Name, EditorStyles.boldLabel);
                var editor = Editor.CreateEditor(_selectedNode);
                editor.OnInspectorGUI();
            }
        }

        private void GenerateGraph()
        {
            _nodeInfos.Clear();
            _groupInfos.Clear();
            Component[] allComponents = _includeInactiveObjects 
                ? Resources.FindObjectsOfTypeAll<Component>()
                : FindObjectsOfType<Component>();

            // First pass: create NodeInfo for each Component and group them
            foreach (Component component in allComponents)
            {
                bool isBuiltIn = IsBuiltInComponent(component);
                if (_includeBuiltInComponents || !isBuiltIn)
                {
                    _nodeInfos[component] = new NodeInfo
                    {
                        IsActive = component.gameObject.activeInHierarchy && (!(component is Behaviour behaviour) || behaviour.enabled),
                        IsBuiltIn = isBuiltIn
                    };

                    System.Type componentType = component.GetType();
                    if (!_groupInfos.ContainsKey(componentType))
                    {
                        _groupInfos[componentType] = new GroupInfo
                        {
                            IsBuiltIn = isBuiltIn
                        };
                    }
                    _groupInfos[componentType].Components.Add(component);
                }
            }

            // Second pass: analyze connections
            foreach (Component component in _nodeInfos.Keys.ToList())
            {
                AnalyzeConnections(component);
            }

            // Third pass: position groups and nodes
            PositionGroupsAndNodes();
        }

        private void PositionGroupsAndNodes()
        {
            float x = 20;
            float y = 20;
            float maxHeight = 0;
            float maxWidth = position.width - 40; // Leave some margin

            foreach (var groupKvp in _groupInfos)
            {
                GroupInfo groupInfo = groupKvp.Value;
                Vector2 groupSize = CalculateGroupSize(groupInfo);

                if (x + groupSize.x > maxWidth)
                {
                    x = 20;
                    y += maxHeight + 40; // Increased vertical spacing between groups
                    maxHeight = 0;
                }

                groupInfo.Position = new Rect(x, y, groupSize.x, groupSize.y);

                // Position nodes within the group
                float nodeX = x + 20;
                float nodeY = y + 40; // Increased top padding
                foreach (Component component in groupInfo.Components)
                {
                    Vector2 nodeSize = CalculateNodeSize(component);
                    _nodeInfos[component].Position = new Rect(nodeX, nodeY, nodeSize.x, nodeSize.y);
                    nodeX += nodeSize.x + 20;
                    if (nodeX + nodeSize.x > x + groupSize.x - 20)
                    {
                        nodeX = x + 20;
                        nodeY += nodeSize.y + 20;
                    }
                }

                x += groupSize.x + 40; // Increased horizontal spacing between groups
                maxHeight = Mathf.Max(maxHeight, groupSize.y);
            }
        }


        private void DrawGraph()
        {
            if (Event.current.type == EventType.Repaint)
            {
                DrawConnections();
                foreach (var groupKvp in _groupInfos)
                {
                    DrawGroup(groupKvp.Key, groupKvp.Value);
                }
                foreach (var kvp in _nodeInfos)
                {
                    DrawNode(kvp.Key, kvp.Value);
                }
            }
        }

        private void DrawGroup(System.Type groupType, GroupInfo groupInfo)
        {
            var scaledRect = ScaleRect(groupInfo.Position);
            
            // Set color based on built-in status
            GUI.color = groupInfo.IsBuiltIn ? new Color(0.8f, 0.9f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);
            
            GUI.Box(scaledRect, "");
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 5, scaledRect.width, 20), groupType.Name, style);
        }

        private void DrawNode(Component component, NodeInfo info)
        {
            var scaledRect = ScaleRect(info.Position);
            
            // Set color based on component type and active state
            if (info.IsBuiltIn)
                GUI.color = info.IsActive ? new Color(0.8f, 0.9f, 1f) : new Color(0.6f, 0.7f, 0.8f);
            else
                GUI.color = info.IsActive ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            
            GUI.Box(scaledRect, "");
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 5, scaledRect.width, 20), component.GetType().Name, style);

            style.fontStyle = FontStyle.Normal;
            style.fontSize = 10;
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 25, scaledRect.width, 20), $"In: {info.Inputs.Count}, Out: {info.Outputs.Count}", style);
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 40, scaledRect.width, 20), component.gameObject.name, style);

            if (scaledRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _selectedNode = component;
                    Repaint();
                }
            }
        }

        private Vector2 CalculateGroupSize(GroupInfo groupInfo)
        {
            float width = 40; // Start with padding
            float height = 60; // Header height + padding
            float rowWidth = 0;
            float rowHeight = 0;
            float maxRowWidth = 0;

            foreach (Component component in groupInfo.Components)
            {
                Vector2 nodeSize = CalculateNodeSize(component);
                
                if (rowWidth + nodeSize.x + 20 > position.width * 0.8f) // Limit row width to 80% of window width
                {
                    // Start a new row
                    maxRowWidth = Mathf.Max(maxRowWidth, rowWidth);
                    height += rowHeight + 20;
                    rowWidth = nodeSize.x + 20;
                    rowHeight = nodeSize.y;
                }
                else
                {
                    rowWidth += nodeSize.x + 20;
                    rowHeight = Mathf.Max(rowHeight, nodeSize.y);
                }
            }

            // Add the last row
            maxRowWidth = Mathf.Max(maxRowWidth, rowWidth);
            height += rowHeight;

            width = Mathf.Max(width, maxRowWidth + 40); // Ensure minimum width and add padding

            return new Vector2(width, height);
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
                if (connectedComponent && _nodeInfos.TryGetValue(connectedComponent, out var info))
                {
                    _nodeInfos[component].Outputs.Add(connectedComponent);
                    info.Inputs.Add(component);
                }
            }
        }

        // Check for connections through GameObject
        foreach (Component otherComponent in component.gameObject.GetComponents<Component>())
        {
            if (otherComponent != component && _nodeInfos.TryGetValue(otherComponent, out var info))
            {
                _nodeInfos[component].Outputs.Add(otherComponent);
                info.Inputs.Add(component);
            }
        }
    }

    private void DrawConnections()
    {
        foreach (var kvp in _nodeInfos)
        {
            Component component = kvp.Key;
            NodeInfo info = kvp.Value;
            Rect startRect = ScaleRect(info.Position);

            foreach (Component connectedComponent in info.Outputs)
            {
                if (_nodeInfos.TryGetValue(connectedComponent, out NodeInfo connectedInfo))
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
            _zoomLevel = Mathf.Clamp(_zoomLevel - Event.current.delta.y * 0.01f, 0.1f, 2f);
            Event.current.Use();
            Repaint();
        }

        if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
        {
            _graphOffset += Event.current.delta;
            Event.current.Use();
            Repaint();
        }
    }

    private Rect ScaleRect(Rect original)
    {
        return new Rect(
            (original.x + _graphOffset.x) * _zoomLevel,
            (original.y + _graphOffset.y) * _zoomLevel,
            original.width * _zoomLevel,
            original.height * _zoomLevel
        );
    }
}
}
