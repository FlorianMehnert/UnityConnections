using JetBrains.Annotations;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor.Nodes
{
    [UsedImplicitly]
    public sealed class GameObjectNode : Node
    {

        private GameObjectNode()
        {
            // Create input and output ports
            var inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
            inputPort.portName = "Input";
            inputContainer.Add(inputPort);

            var outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            outputPort.portName = "Output";
            outputContainer.Add(outputPort);

            // Add a label in the main content area
            Label contentLabel = new("no size");
            contentContainer.Add(contentLabel);

            this.AddManipulator(new ContextualMenuManipulator(BuildContextMenu));
        }

        private void BuildContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("set Size", _ => Debug.Log(contentContainer.worldBound));
            evt.menu.AppendAction("Action 2", _ => Debug.Log("Action 2 triggered"), DropdownMenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete Node", _ => RemoveFromHierarchy(), DropdownMenuAction.AlwaysEnabled);
        }
    }
}