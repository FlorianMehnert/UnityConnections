using System.Collections.Generic;
using SceneConnections.Editor.Utils;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneConnections.Editor.Nodes
{
    public sealed class AdvancedNode : Node
    {
        internal AdvancedNode()
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
            evt.menu.AppendAction("Color connected nodes", _ => ColorConnectedNodes(Color.red));
        }
        private void ColorConnectedNodes(Color color)
        {
            var visitedNodes = new HashSet<Node>();
            NodeUtils.ResetNodeColors();
            NodeUtils.TraverseConnectedNodes(this, color, visitedNodes);
        }
    }
}