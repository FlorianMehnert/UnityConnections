using UnityEngine;

public static class TransformExtensions
{
    public static int GetHierarchyDepth(this Transform transform)
    {
        int depth = 0;
        Transform current = transform;

        // Traverse upwards through parent objects, counting levels
        while (current.parent != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }
}