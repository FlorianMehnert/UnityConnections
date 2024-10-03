using UnityEngine;
using XNode;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // For async threading
using UnityEditor;

[CreateAssetMenu]
public class SceneComponentGraph : NodeGraph
{
    private List<(Component component, GameObject go)> componentData = new List<(Component, GameObject)>();
    private Dictionary<Component, ComponentNode> nodeMap = new Dictionary<Component, ComponentNode>();

    public void GenerateGraphAsync(GameObject[] gameObjects)
    {
        // Clear existing graph data
        Clear();

        // Start the background task to gather components
        Task.Run(() =>
        {
            GatherComponentData(gameObjects);
        })
        .ContinueWith(task =>
        {
            // After gathering data, schedule node creation on the main thread
            EditorApplication.update += CreateNodesAndConnect;
        });
    }

    // Background thread - gather component data (thread-safe)
    private void GatherComponentData(GameObject[] gameObjects)
    {
        componentData.Clear();

        foreach (GameObject go in gameObjects)
        {
            Component[] components = go.GetComponents<Component>();

            foreach (Component component in components)
            {
                componentData.Add((component, go));
            }
        }

        Debug.Log($"Collected {componentData.Count} components from {gameObjects.Length} GameObjects");
    }

    // Main thread - create nodes and connect them (not thread-safe, must be run on the main thread)
    private void CreateNodesAndConnect()
    {
        // Remove this callback to avoid repeated calls
        EditorApplication.update -= CreateNodesAndConnect;

        nodeMap.Clear();

        // Create nodes for components
        foreach (var data in componentData)
        {
            Component component = data.component;
            GameObject go = data.go;

            ComponentNode node = AddNode<ComponentNode>();
            node.target = component;
            node.name = $"{go.name} - {component.GetType().Name}";
            node.componentType = component.GetType().Name;

            nodeMap[component] = node;
        }

        // Connect the nodes based on component relationships
        foreach (var kvp in nodeMap)
        {
            Component component = kvp.Key;
            ComponentNode node = kvp.Value;
            Component[] referencedComponents = FindReferencedComponents(component);

            foreach (Component referenced in referencedComponents)
            {
                if (nodeMap.TryGetValue(referenced, out ComponentNode referencedNode))
                {
                    node.GetOutputPort("output").Connect(referencedNode.GetInputPort("input"));
                }
            }
        }

        // Scatter the nodes in the editor
        ScatterNodes();
    }

    private Component[] FindReferencedComponents(Component component)
    {
        // Your existing logic for finding referenced components
        return new Component[0];
    }

    private void ScatterNodes()
    {
        List<Node> nodes = this.nodes.ToList();
        int nodeCount = nodes.Count;
        Debug.Log($"Scattering {nodeCount} nodes");

        float radius = Mathf.Sqrt(nodeCount) * 100;
        for (int i = 0; i < nodeCount; i++)
        {
            float angle = i * (2 * Mathf.PI / nodeCount);
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            x += Random.Range(-50f, 50f);
            y += Random.Range(-50f, 50f);

            nodes[i].position = new Vector2(x, y);
        }
    }
}
