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

    public static T TryGetNodeAtPosition<T>(this Node contextNode, Vector2I tilePosition) where T : Node2D
    {
        var ySortRoot = contextNode.GetTree()?.CurrentScene?.GetNodeOrNull<Node2D>("YSortRoot")
            ?? contextNode.GetParent()?.GetNodeOrNull<Node2D>("YSortRoot");
        if (ySortRoot == null)
        {
            return null;
        }

        var nodesToVisit = new Queue<Node>();
        nodesToVisit.Enqueue(ySortRoot);

        while (nodesToVisit.Count > 0)
        {
            var current = nodesToVisit.Dequeue();

            foreach (Node child in current.GetChildren())
            {
                nodesToVisit.Enqueue(child);

                if (child is not T node2D || child is TileMapLayer)
                {
                    continue;
                }

                var tileX = (int)Mathf.Floor(node2D.GlobalPosition.X / 64f);
                var tileY = (int)Mathf.Floor(node2D.GlobalPosition.Y / 64f);
                if (new Vector2I(tileX, tileY) == tilePosition)
                {
                    return node2D;
                }
            }
        }

        return null;
    }
}
