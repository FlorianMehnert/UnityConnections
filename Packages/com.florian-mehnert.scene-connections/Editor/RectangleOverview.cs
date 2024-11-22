using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneConnections.Editor
{
    public class RectangleWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private float _zoomLevel = 1f;
        private Vector2 _graphOffset;
        private readonly Dictionary<Component, NodeInfo> _nodeInfos = new();
        private readonly Dictionary<Type, GroupInfo> _groupInfos = new();
        private Component _selectedNode;
        private bool _includeInactiveObjects = true;
        private bool _includeBuiltInComponents = true;
        private bool _showEqualComponents = true;
        private bool _graphNeedsUpdate = true;
        private bool _isDragging;
        private Component _draggedNode;
        private Vector2 _dragOffset;

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
            public readonly List<Component> Components = new();
            public bool IsBuiltIn;
        }

        private void OnEnable()
        {
            // Subscribe to Unity's update event
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            // Unsubscribe from Unity's update event
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // Check if any relevant changes have occurred in the scene
            if (!CheckForSceneChanges()) return;
            _graphNeedsUpdate = true;
            Repaint();
        }

        private static bool CheckForSceneChanges()
        {
            // Implement logic to check for relevant changes in the scene
            // For example, check if any GameObjects or Components have been added/removed/modified
            // Return true if changes are detected, false otherwise
            return SceneManager.GetActiveScene().isDirty;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Graph"))
            {
                GenerateGraph();
                _graphNeedsUpdate = false;
            }

            var newIncludeInactive = EditorGUILayout.ToggleLeft("Include Inactive", _includeInactiveObjects);
            var newIncludeBuiltIn = EditorGUILayout.ToggleLeft("Include Built-in", _includeBuiltInComponents);
            var newShowEqualComponents = EditorGUILayout.ToggleLeft("Include Equal Components", _showEqualComponents);

            if (newIncludeInactive != _includeInactiveObjects ||
                newIncludeBuiltIn != _includeBuiltInComponents ||
                newShowEqualComponents != _showEqualComponents)
            {
                _includeInactiveObjects = newIncludeInactive;
                _includeBuiltInComponents = newIncludeBuiltIn;
                _showEqualComponents = newShowEqualComponents;
                _graphNeedsUpdate = true;
            }

            EditorGUILayout.EndHorizontal();

            HandleEvents();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_graphNeedsUpdate)
            {
                GenerateGraph();
                _graphNeedsUpdate = false;
            }

            DrawGraph();
            EditorGUILayout.EndScrollView();

            if (_selectedNode)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Selected: " + _selectedNode.GetType().Name, EditorStyles.boldLabel);
            }
        }

        private Component GetNodeAtPosition(Vector2 mousePosition)
        {
            return (from kvp in _nodeInfos let scaledRect = ScaleRect(kvp.Value.Position) where scaledRect.Contains(mousePosition) select kvp.Key).FirstOrDefault();
        }

        private void HandleEvents()
        {
            var e = Event.current;

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    var prevZoom = _zoomLevel;
                    _zoomLevel = Mathf.Clamp(_zoomLevel - e.delta.y * 0.01f, 0.1f, 2f);
                    if (!Mathf.Approximately(prevZoom, _zoomLevel))
                    {
                        e.Use();
                        Repaint();
                    }

                    break;

                case EventType.MouseDown:
                    switch (e.button)
                    {
                        // Middle mouse button for graph dragging
                        case 2:
                            _isDragging = true;
                            e.Use();
                            break;
                        // Left mouse button for selecting/moving nodes
                        case 0:
                        {
                            _draggedNode = GetNodeAtPosition(e.mousePosition);
                            if (_draggedNode != null)
                            {
                                _dragOffset = e.mousePosition - ScaleRect(_nodeInfos[_draggedNode].Position).position;
                                _selectedNode = _draggedNode;
                                e.Use();
                            }

                            break;
                        }
                    }

                    break;

                case EventType.MouseDrag:
                    if (_isDragging) // Graph dragging
                    {
                        _graphOffset += e.delta;
                        e.Use();
                        Repaint();
                    }
                    else if (_draggedNode) // Node dragging
                    {
                        _nodeInfos[_draggedNode].Position.position = (e.mousePosition - _dragOffset) / _zoomLevel - _graphOffset;
                        e.Use();
                        Repaint();
                    }

                    break;

                case EventType.MouseUp:
                    switch (e.button)
                    {
                        // Stop dragging graph
                        case 2:
                            _isDragging = false;
                            e.Use();
                            break;
                        // Stop dragging node
                        case 0:
                            _draggedNode = null;
                            e.Use();
                            break;
                    }

                    break;
                case EventType.MouseMove:
                    break;
                case EventType.KeyDown:
                    break;
                case EventType.KeyUp:
                    break;
                case EventType.Repaint:
                    break;
                case EventType.Layout:
                    break;
                case EventType.DragUpdated:
                    break;
                case EventType.DragPerform:
                    break;
                case EventType.DragExited:
                    break;
                case EventType.Ignore:
                    break;
                case EventType.Used:
                    break;
                case EventType.ValidateCommand:
                    break;
                case EventType.ExecuteCommand:
                    break;
                case EventType.ContextClick:
                    break;
                case EventType.MouseEnterWindow:
                    break;
                case EventType.MouseLeaveWindow:
                    break;
                case EventType.TouchDown:
                    break;
                case EventType.TouchUp:
                    break;
                case EventType.TouchMove:
                    break;
                case EventType.TouchEnter:
                    break;
                case EventType.TouchLeave:
                    break;
                case EventType.TouchStationary:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawGraph()
        {
            if (Event.current.type != EventType.Repaint) return;
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


        [MenuItem("Window/Connections v1 #&1")]
        public static void ShowWindow()
        {
            var window = GetWindow<RectangleWindow>("Rectangle Graph");
            window.minSize = new Vector2(800, 600);
        }

        [Shortcut("MyGraphEditor/GenerateGraphShortcut", KeyCode.R, ShortcutModifiers.Shift)]
        private static void GenerateGraphShortcut()
        {
            var window = GetWindow<RectangleWindow>();
            if (window == null) return;
            window.GenerateGraph();
            window._graphNeedsUpdate = false;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void GenerateGraph()
        {
            _nodeInfos.Clear();
            _groupInfos.Clear();
            var allComponents = _includeInactiveObjects
                ? Resources.FindObjectsOfTypeAll<Component>()
                : FindObjectsByType<Component>(FindObjectsSortMode.InstanceID);

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

                    var componentType = component.GetType();
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
            var maxWidth = position.width - 40; // Leave some margin

            foreach (var groupKvp in _groupInfos)
            {
                var groupInfo = groupKvp.Value;
                var groupSize = CalculateGroupSize(groupInfo);

                if (x + groupSize.x > maxWidth)
                {
                    x = 20;
                    y += maxHeight + 40; // Increased vertical spacing between groups
                    maxHeight = 0;
                }

                groupInfo.Position = new Rect(x, y, groupSize.x, groupSize.y);

                // Position nodes within the group
                var nodeX = x + 20;
                var nodeY = y + 40; // Increased top padding
                foreach (var component in groupInfo.Components)
                {
                    var nodeSize = CalculateNodeSize(component);
                    _nodeInfos[component].Position = new Rect(nodeX, nodeY, nodeSize.x, nodeSize.y);
                    nodeX += nodeSize.x + 20;
                    if (!(nodeX + nodeSize.x > x + groupSize.x - 20)) continue;
                    nodeX = x + 20;
                    nodeY += nodeSize.y + 20;
                }

                x += groupSize.x + 40; // Increased horizontal spacing between groups
                maxHeight = Mathf.Max(maxHeight, groupSize.y);
            }
        }

        private void DrawGroup(Type groupType, GroupInfo groupInfo)
        {
            var scaledRect = ScaleRect(groupInfo.Position);

            // Set color based on built-in status
            GUI.color = groupInfo.IsBuiltIn ? new Color(0.8f, 0.9f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.5f);

            GUI.Box(scaledRect, "");
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
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
                GUI.color = info.IsActive ? new Color(0.1f, 0.9f, 1f) : new Color(0.9f, 0.2f, 0.1f);
            else
                GUI.color = info.IsActive ? Color.white : new Color(0.7f, 0.1f, 0.7f);

            GUI.Box(scaledRect, "");
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 5, scaledRect.width, 20), component.GetType().Name, style);

            style.fontStyle = FontStyle.Normal;
            style.fontSize = 10;
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 25, scaledRect.width, 20), $"In: {info.Inputs.Count}, Out: {info.Outputs.Count}", style);
            GUI.Label(new Rect(scaledRect.x, scaledRect.y + 40, scaledRect.width, 20), component.gameObject.name, style);

            if (!scaledRect.Contains(Event.current.mousePosition)) return;
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
            _selectedNode = component;

            // might be causing some lag
            Repaint();
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
                    // New row
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

            // Last row
            maxRowWidth = Mathf.Max(maxRowWidth, rowWidth);
            height += rowHeight;

            width = Mathf.Max(width, maxRowWidth + 40); // Ensure minimum width and add padding

            return new Vector2(width, height);
        }


        private static bool IsBuiltInComponent(Component component)
        {
            var componentType = component.GetType();
            var namespaceName = componentType.Namespace;

            // Check if the component is from Unity's built-in namespaces
            if (!string.IsNullOrEmpty(namespaceName) &&
                (namespaceName.StartsWith("UnityEngine") || namespaceName.StartsWith("UnityEditor")))
            {
                return true;
            }

            // Check if the component is a built-in Unity component without a namespace
            string[] builtInComponents =
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
            var fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!typeof(Component).IsAssignableFrom(field.FieldType)) continue;
                var connectedComponent = field.GetValue(component) as Component;
                if (!connectedComponent || !_nodeInfos.TryGetValue(connectedComponent, out var info)) continue;
                _nodeInfos[component].Outputs.Add(connectedComponent);
                info.Inputs.Add(component);
            }

            // Check for connections through GameObject
            foreach (var otherComponent in component.gameObject.GetComponents<Component>())
            {
                if (otherComponent == component || !_nodeInfos.TryGetValue(otherComponent, out var info)) continue;
                _nodeInfos[component].Outputs.Add(otherComponent);
                info.Inputs.Add(component);
            }
        }

        private void DrawConnections()
        {
            foreach (var (component, info) in _nodeInfos)
            {
                var startRect = ScaleRect(info.Position);

                foreach (var connectedComponent in info.Outputs)
                {
                    if (!_nodeInfos.TryGetValue(connectedComponent, out NodeInfo connectedInfo)) continue;
                    var endRect = ScaleRect(connectedInfo.Position);
                    if (connectedComponent.gameObject == component.gameObject)
                    {
                        if (_showEqualComponents)
                        {
                            DrawConnectionLine(GetConnector(startRect, false), GetConnector(endRect, true), Color.green);
                        }
                    }
                    else
                    {
                        DrawConnectionLine(GetConnector(startRect, false), GetConnector(endRect, true), Color.blue);
                    }
                }
            }
        }

        /// <summary>
        /// Get the Position of the left/right connector
        /// </summary>
        /// <param name="rect">rectangle for which to calculate connector positions</param>
        /// <param name="left">return left connector if <c>true</c> and right connector if <c>false</c></param>
        /// <returns></returns>
        private static Vector2 GetConnector(Rect rect, bool left)
        {
            return new Vector2(left ? rect.x : rect.x + rect.width, rect.center.y);
        }

        /// <summary>
        /// Connects two nodes together defined by start and end using a Bézier curve
        /// </summary>
        /// <param name="start">Vector2 defining the start of the connection</param>
        /// <param name="end">Vector2 defining the end of the connection</param>
        /// <param name="color">color of the connection</param>
        private static void DrawConnectionLine(Vector2 start, Vector2 end, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawBezier(start, end, start + Vector2.right * 50, end - Vector2.right * 50, color, null, 4f);
            Handles.EndGUI();
        }

        /// <summary>
        /// calculates width based on Name of node
        /// </summary>
        /// <param name="component">mostly nodes should be inserted in here</param>
        /// <returns><c>Vector2</c> containing width and height of the component</returns>
        private static Vector2 CalculateNodeSize(Component component)
        {
            var width = Mathf.Max(GUI.skin.box.CalcSize(new GUIContent(component.GetType().Name)).x + 20, 120);
            const float height = 75;
            return new Vector2(width, height);
        }

        /// <summary>
        /// Method which returns a newly translated and scaled rectangle based on offset and scaling
        /// based on <c>_graphOffset</c> and <c>_zoomLevel</c>
        /// </summary>
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