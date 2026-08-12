using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game;

public static class NodeExtensions
{
    public static T GetFirstNodeOfType<T>(this Node node) where T : Node
    {
        var children = node.GetChildren();
        var firstNode = children.FirstOrDefault((child) => child is T);
        return firstNode as T;
    }

    public static List<T> GetNodesOfType<T>(this Node root) where T : Node
    {
        List<T> results = new();
    
        foreach (Node child in root.GetChildren())
        {
            if (child is T typedChild)
                results.Add(typedChild);

            results.AddRange(GetNodesOfType<T>(child));
        }

        return results;
    }
}
