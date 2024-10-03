using XNode;
using UnityEngine;

public class ComponentNode : Node 
{
    [Input] public Component input;
    [Output] public Component output;

    public Component target;
    public string componentType;

    private void OnValidate() 
    {
        if (target != null && componentType != target.GetType().Name) 
        {
            componentType = target.GetType().Name;
            // Notify the graph about the change
            Debug.Log($"ComponentNode updated: {name}");
        }
    }

    public override object GetValue(NodePort port) 
    {
        return target;
    }
}