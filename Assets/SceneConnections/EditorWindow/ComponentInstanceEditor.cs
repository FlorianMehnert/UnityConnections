using UnityEditor;
using UnityEngine;

namespace SceneConnections.EditorWindow
{
    public class ComponentInstanceEditor : UnityEditor.EditorWindow
    {
        private System.Type _componentType;
        private Component _selectedComponent;
        private Vector2 _scrollPosition;

        public static void OpenWindow(System.Type componentType)
        {
            ComponentInstanceEditor window = GetWindow<ComponentInstanceEditor>();
            window.titleContent = new GUIContent($"Edit {componentType.Name}");
            window._componentType = componentType;
            window.Show();
        }

        private void OnGUI()
        {
            if (_componentType == null)
            {
                EditorGUILayout.LabelField("No component type selected.");
                return;
            }

            EditorGUILayout.LabelField($"Editing {_componentType.Name}", EditorStyles.boldLabel);

            _selectedComponent = EditorGUILayout.ObjectField("Select Instance", _selectedComponent, _componentType, true) as Component;

            if (_selectedComponent != null)
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                Editor editor = Editor.CreateEditor(_selectedComponent);
                editor.DrawDefaultInspector();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("Select an instance of the component to edit.", MessageType.Info);
            }
        }
    }
}