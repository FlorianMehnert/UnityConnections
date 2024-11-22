using SceneConnections.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class ComponentGraphViewer : EditorWindow
    {
        private ComponentGraphView _graphView;

        private void OnEnable()
        {
            var highlightColor = new Color(1f, 0.8f, 0.2f, 1f);
            var defaultColor = new Color(0.2f, 0.2f, 0.2f, .5f);
            _graphView = new ComponentGraphView(defaultColor, highlightColor);
            rootVisualElement.Add(_graphView);

            var setComponentGraphDrawType = new DropdownField("Set Component Graph Draw Type")
            {
                choices = { "nodes are components", "nodes are game objects", "nodes are scripts" },
                value = "nodes are scripts"
            };
            setComponentGraphDrawType.RegisterValueChangedCallback(evt => { _graphView.GraphDrawType = Constants.ToCgdt(evt.newValue); });
            rootVisualElement.Add(setComponentGraphDrawType);

            var refreshButton = new Button(() =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        _graphView.RefreshGraph();
                    };
                })
                { text = "Refresh Graph" };
            rootVisualElement.Add(refreshButton);
            _graphView.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        }


        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }
        
        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Center on connected nodes", _ => GraphViewUtilities.CenterCameraOnSelectedNodes(_graphView));
        }

        [MenuItem("Window/Connections v2 #&2")]
        public static void OpenWindow()
        {
            var window = GetWindow<ComponentGraphViewer>();
            window.titleContent = new GUIContent("Enhanced Component Graph");
            window.minSize = new Vector2(800, 600);
        }
    }
}