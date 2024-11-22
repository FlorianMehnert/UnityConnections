using System.Collections.Generic;
using System.Linq;
using SceneConnections.Editor.Utils;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace SceneConnections.Editor
{
    public interface IConnectionGraphView
    {
        public void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            var searchText = evt.newValue.ToLowerInvariant();
            SearchNodes(searchText);
        }

        public void Add(VisualElement element)
        {
            throw new System.NotImplementedException();
        }

        public void AddElement(VisualElement element)
        {
            throw new System.NotImplementedException();
        }

        public void DeleteElements(List<GraphElement> gvGraphElements)
        {
            throw new System.NotImplementedException();
        }

        private void SearchNodes(string searchText)
        {
            if (Nodes == null) return;
            foreach (var node in Nodes)
            {
                ResetNodeColor(node);
            }

            if (string.IsNullOrEmpty(searchText))
                return;

            var matchingNodes = Nodes.Where(n =>
                ContainsText(n, searchText) ||
                IsCustomNodeMatch(n, searchText)
            );

            foreach (var node in matchingNodes)
            {
                HighlightNode(node);
            }
        }

        // TODO: reset to actual background color
        /// <summary>
        /// Set node back to original background
        /// </summary>
        /// <param name="node"></param>
        private void ResetNodeColor(Node node)
        {
            node.style.backgroundColor = DefaultNodeColor;
            node.MarkDirtyRepaint();
        }


        private static bool IsCustomNodeMatch(Node node, string searchText)
        {
            return node != null && node.title.ToLowerInvariant().Contains(searchText);
        }

        // TODO: change searching based on some checkboxes
        private static bool ContainsText(Node node, string searchText)
        {
            return node.title.ToLowerInvariant().Contains(searchText);
        }

        /// <summary>
        /// Highlight node on search hit
        /// </summary>
        /// <param name="node">node to be highlighted</param>
        private void HighlightNode(Node node)
        {
            node.style.backgroundColor = HighlightColor;
            node.MarkDirtyRepaint();
        }

        StyleColor HighlightColor { get; }

        List<Node> Nodes { get; }
        StyleColor DefaultNodeColor { get; }

        string PathTextFieldValue { get; set; }
        public TextField PathTextField { get; set; }
        TextField SearchField { get; set; }

        public bool IsBusy { get; set; }
        public List<GraphElement> GraphElements { get; }

        public NodeGraphBuilder NodeGraphBuilder { get; }
        
        public bool ReferenceInheritance { get; set; }
        public bool ReferenceFields { get; set; }
        public bool ReferenceMethods { get; set; }
    }
}