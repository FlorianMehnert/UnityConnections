using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public class NavigableMinimap : MiniMap
    {
        private readonly GraphView _parentGraphView;
        private Vector2 _dragStartPosition;
        private Vector2 _viewStartPosition;
        private bool _isDragging;
        private float _scaleX;
        private float _scaleY;
        private bool _isMinimized;
        private bool _isVisible = true;

        public NavigableMinimap(GraphView graphView)
        {
            _parentGraphView = graphView;
            SetupCallbacks();

            anchored = true;
            style.display = DisplayStyle.Flex;

            // Calculate initial scale factors
            UpdateScaleFactors();
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenuPopulate);
        }

        private void FoldMinimap()
        {
            _isMinimized = true;
            style.width = 50;   // Shrink the minimap width
            style.height = 50;  // Shrink the minimap height
        }

        private void UnfoldMinimap()
        {
            _isMinimized = false;
            style.width = StyleKeyword.Auto;   // Restore original size
            style.height = StyleKeyword.Auto;
        }

        private void ShowMinimap()
        {
            _isVisible = true;
            style.display = DisplayStyle.Flex;
        }

        private void HideMinimap()
        {
            _isVisible = false;
            style.display = DisplayStyle.None;
        }

        private void RemoveMinimap()
        {
            // Remove the minimap completely from the parent container
            parent?.Remove(this);
        }


        private void SetupCallbacks()
        {
            RegisterCallback<GeometryChangedEvent>(_ => UpdateScaleFactors());

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return; // Left mouse button
                _isDragging = true;
                _dragStartPosition = this.WorldToLocal(evt.mousePosition);
                _viewStartPosition = _parentGraphView.viewTransform.position;

                // Capture the mouse to continue receiving events
                this.CaptureMouse();
                evt.StopPropagation();
            });

            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!_isDragging) return;
                _isDragging = false;
                this.ReleaseMouse();
                evt.StopPropagation();
            });

            RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!_isDragging) return;
                var currentMousePos = this.WorldToLocal(evt.mousePosition);
                var dragDelta = currentMousePos - _dragStartPosition;

                // Apply scaled delta for smoother movement
                Vector2 scaledDelta = new(
                    dragDelta.x * 6.0f / _scaleX,
                    dragDelta.y * 4.0f / _scaleY
                );

                _parentGraphView.viewTransform.position = _viewStartPosition - scaledDelta;
                evt.StopPropagation();
            });
        }
        
        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Show Minimap", _ => ShowMinimap(), _isVisible ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Hide Minimap", _ => HideMinimap(), _isVisible ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        
            if (_isMinimized)
            {
                evt.menu.AppendAction("Unfold Minimap", _ => UnfoldMinimap());
            }
            else
            {
                evt.menu.AppendAction("Fold Minimap", _ => FoldMinimap());
            }
        
            evt.menu.AppendAction("Remove Minimap", _ => RemoveMinimap());
        }

        private void UpdateScaleFactors()
        {
            _scaleX = contentRect.width / _parentGraphView.contentRect.width;
            _scaleY = contentRect.height / _parentGraphView.contentRect.height;
        }
    }
}