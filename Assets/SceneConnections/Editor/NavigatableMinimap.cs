using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.EditorWindow
{
    // TODO: enter state where landing on the minimap after drag does not move the minimap to the landed spot
    // TODO: make default not floating
    public class NavigableMinimap : MiniMap
    {
        private readonly GraphView _parentGraphView;
        private Vector2 _dragStartPosition;
        private Vector2 _viewStartPosition;
        private bool _isDragging;

        public NavigableMinimap(GraphView graphView)
        {
            _parentGraphView = graphView;
            SetupCallbacks();
        
            // Set default minimap style
            style.width = 200;
            style.height = 200;
            style.position = Position.Absolute;
            style.right = 10;
            style.top = 10;
        }

        private void SetupCallbacks()
        {
            // Handle mouse down for both click navigation and drag start
            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left mouse button
                {
                    _isDragging = true;
                    _dragStartPosition = this.WorldToLocal(evt.mousePosition);
                    _viewStartPosition = _parentGraphView.viewTransform.position;
                
                    // Capture the mouse
                    this.CaptureMouse();
                    evt.StopPropagation();
                }
            });

            // Handle mouse up to end dragging
            RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 0 && _isDragging)
                {
                    _isDragging = false;
                    this.ReleaseMouse();
                    evt.StopPropagation();
                }
            });

            // Handle direct clicks for instant navigation
            RegisterCallback<ClickEvent>(evt =>
            {
                if (!_isDragging) // Only handle clicks that weren't part of a drag
                {
                    // Calculate the relative position within the minimap
                    Vector2 localPos = this.WorldToLocal(evt.localPosition);
                    Vector2 normalizedPos = new Vector2(
                        localPos.x / contentRect.width,
                        localPos.y / contentRect.height
                    );

                    // Calculate the target position in the main view
                    Vector2 targetPos = new Vector2(
                        -normalizedPos.x * _parentGraphView.contentRect.width,
                        -normalizedPos.y * _parentGraphView.contentRect.height
                    );

                    // Center the view on the clicked position
                    targetPos += new Vector2(
                        _parentGraphView.viewTransform.scale.x * _parentGraphView.contentRect.width / 2,
                        _parentGraphView.viewTransform.scale.y * _parentGraphView.contentRect.height / 2
                    );

                    // Animate to the target position
                    _parentGraphView.viewTransform.position = Vector2.Lerp(
                        _parentGraphView.viewTransform.position,
                        targetPos,
                        0.8f
                    );
                }
            });
            
            RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (_isDragging)
                {
                    Vector2 currentMousePos = this.WorldToLocal(evt.mousePosition);
                    Vector2 dragDelta = currentMousePos - _dragStartPosition;
                
                    // Calculate the scale factor between minimap and main view
                    float scaleX = contentRect.width / _parentGraphView.contentRect.width;
                    float scaleY = contentRect.height / _parentGraphView.contentRect.height;
                
                    // Apply inverse scaling to the drag delta
                    Vector2 scaledDelta = new Vector2(
                        dragDelta.x / scaleX,
                        dragDelta.y / scaleY
                    );
                
                    // Update the main view position
                    _parentGraphView.viewTransform.position = _viewStartPosition - scaledDelta;
                
                    evt.StopPropagation();
                }
            });
        }
    }
}
