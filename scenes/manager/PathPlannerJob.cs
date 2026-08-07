using System;
using System.Collections.Generic;
using System.Linq;
using Game.Component;
using Godot;

namespace Game.Manager;

/// <summary>
/// A resumable A* planner job that can be advanced a few iterations per frame.
/// Contains no Godot-specific nodes beyond using GridManager for move checks.
/// </summary>
public class PathPlannerJob
{
    private readonly GridManager gridManager;
    private readonly BuildingComponent robot;
    private readonly bool allowBridges;
    private readonly bool? bridgeElevationIsElevated;
    private readonly HashSet<Vector2I> excludedPositions;
    private readonly Vector2I target;

    private List<PathNode> open = new();
    private HashSet<Vector2I> closed = new();

    public bool Completed { get; private set; } = false;
    public List<Vector2I> Result { get; private set; } = null;
    public Action<List<Vector2I>> OnComplete { get; }

    public PathPlannerJob(GridManager gridManager, BuildingComponent robot, Vector2I start, Vector2I target, bool allowBridges, bool? bridgeElevationIsElevated, HashSet<Vector2I> excludedPositions, Action<List<Vector2I>> onComplete)
    {
        this.gridManager = gridManager ?? throw new ArgumentNullException(nameof(gridManager));
        this.robot = robot;
        this.target = target;
        this.allowBridges = allowBridges;
        this.bridgeElevationIsElevated = bridgeElevationIsElevated;
        this.excludedPositions = excludedPositions;
        this.OnComplete = onComplete;

        open.Add(new PathNode(start, null, 0, Heuristic(start, target)));
    }

    public void Step(int maxIterations)
    {
        if (Completed) return;

        int iterations = 0;
        while (open.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            open.Sort((a, b) => a.F.CompareTo(b.F));
            var current = open[0];
            open.RemoveAt(0);
            closed.Add(current.Position);

            if (current.Position == target)
            {
                // reconstruct
                var path = new List<Vector2I>();
                var node = current;
                while (node != null)
                {
                    path.Add(node.Position);
                    node = node.Parent;
                }
                path.Reverse();
                Result = path;
                Completed = true;
                OnComplete?.Invoke(Result);
                return;
            }

            var neighbors = new[]
            {
                new Vector2I(current.Position.X, current.Position.Y - 1), // Up
                new Vector2I(current.Position.X, current.Position.Y + 1), // Down
                new Vector2I(current.Position.X - 1, current.Position.Y), // Left
                new Vector2I(current.Position.X + 1, current.Position.Y)  // Right
            };

            foreach (var neighborPos in neighbors)
            {
                if (closed.Contains(neighborPos)) continue;
                if (excludedPositions != null && excludedPositions.Contains(neighborPos)) continue;

                Rect2I originArea = new Rect2I(current.Position, Vector2I.One);
                Rect2I destinationArea = new Rect2I(neighborPos, Vector2I.One);

                if (!gridManager.IsBuildingMovable(robot, originArea, destinationArea, allowBridges, bridgeElevationIsElevated))
                {
                    continue;
                }

                int gCost = current.G + 1;
                int hCost = Heuristic(neighborPos, target);

                var existing = open.FirstOrDefault(n => n.Position == neighborPos);
                if (existing != null)
                {
                    if (gCost < existing.G)
                    {
                        existing.G = gCost;
                        existing.Parent = current;
                    }
                }
                else
                {
                    open.Add(new PathNode(neighborPos, current, gCost, hCost));
                }
            }
        }

        // If we exhausted open list without finding target, mark completed with null result
        if (open.Count == 0 && !Completed)
        {
            Completed = true;
            Result = null;
            OnComplete?.Invoke(null);
        }
    }

    private int Heuristic(Vector2I from, Vector2I to)
    {
        return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
    }

    private class PathNode
    {
        public Vector2I Position;
        public PathNode Parent;
        public int G;
        public int H;
        public int F => G + H;

        public PathNode(Vector2I pos, PathNode parent, int g, int h)
        {
            Position = pos;
            Parent = parent;
            G = g;
            H = h;
        }
    }
}
