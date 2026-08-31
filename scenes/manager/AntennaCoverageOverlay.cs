using System;
using System.Collections.Generic;
using System.Linq;
using Game.Autoload;
using Game.Component;
using Godot;

namespace Game.Manager;

/// <summary>
/// Draws a proximity-sensitive edge around the connected antenna network.
/// It deliberately uses vector drawing instead of the highlight TileMap so
/// terrain, paths, and interaction highlights remain readable.
/// </summary>
public partial class AntennaCoverageOverlay : Node2D
{
	private const float TileSize = 64.0f;
	private static readonly Color BaseAndRelayColor = new(0.12f, 0.62f, 1.0f);
	private static readonly Color SelectedRelayColor = new(0.22f, 1.0f, 0.40f);

	[Export]
	public float MinimumAlpha { get; set; } = 0.035f;
	[Export]
	public float MaximumAlpha { get; set; } = 0.72f;
	[Export]
	public float SelectedMinimumAlpha { get; set; } = 0.30f;
	[Export]
	public float ProximityFadeDistanceInTiles { get; set; } = 24.0f;
	[Export]
	public float CoreLineWidth { get; set; } = 3.0f;
	[Export]
	public float GlowLineWidth { get; set; } = 14.0f;
	[Export]
	public float BoundaryTileFillStrength { get; set; } = 0.14f;

	[Export]
	private GridManager gridManager;
	private BuildingComponent selectedBuilding;
	private bool initialized;

	public override void _Ready()
	{
		Initialize(gridManager);
	}

	public void Initialize(GridManager ownerGridManager)
	{
		if (initialized || ownerGridManager == null)
		{
			return;
		}

		initialized = true;
		gridManager = ownerGridManager;
		Material = new CanvasItemMaterial
		{
			BlendMode = CanvasItemMaterial.BlendModeEnum.Mix,
			LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
		};

		GameEvents.Instance.Connect(
			GameEvents.SignalName.BuildingPlaced,
			Callable.From<BuildingComponent>(OnNetworkChanged));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.BuildingMoved,
			Callable.From<BuildingComponent>(OnNetworkChanged));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.BuildingDestroyed,
			Callable.From<BuildingComponent>(OnBuildingDestroyed));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.BuildingEnabled,
			Callable.From<BuildingComponent>(OnNetworkChanged));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.BuildingDisabled,
			Callable.From<BuildingComponent>(OnNetworkChanged));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.RobotSelected,
			Callable.From<BuildingComponent>(OnRobotSelected));
		GameEvents.Instance.Connect(
			GameEvents.SignalName.NoMoreRobotSelected,
			Callable.From<BuildingComponent>(OnRobotSelectionCleared));
	}

	public override void _Draw()
	{
		if (gridManager == null)
		{
			return;
		}

		var observers = BuildingComponent.GetValidBuildingComponents(gridManager)
			.Where(building =>
				!building.BuildingResource.IsBase &&
				building.BuildingResource.DisplayName != "Antenna")
			.Select(GetBuildingCenterInWorld)
			.ToList();

		var layers = gridManager.GetConnectedAntennaCoverageLayers()
			.OrderBy(layer => layer.Building == selectedBuilding ? 1 : 0)
			.ToList();

		foreach (var layer in layers)
		{
			DrawCoverageLayer(layer, observers);
		}
	}

	private void DrawCoverageLayer(
		GridManager.AntennaCoverageVisualLayer layer,
		List<Vector2> observers)
	{
		var buildingArea = layer.Building.GetTileArea();
		var center = GetBuildingCenterInWorld(layer.Building);
		var radiusModifier = Mathf.Max(buildingArea.Size.X, buildingArea.Size.Y) / 2.0f;
		var radius = (layer.Building.BuildingResource.BuildableRadius + radiusModifier) * TileSize;
		var segmentCount = Mathf.Max(96, Mathf.CeilToInt(radius / 7.0f));
		var isSelected = layer.Building == selectedBuilding && !layer.IsBase;
		var color = isSelected ? SelectedRelayColor : BaseAndRelayColor;
		var boundaryTiles = new Dictionary<Vector2I, float>();

		for (var segment = 0; segment < segmentCount; segment++)
		{
			var startAngle = Mathf.Tau * segment / segmentCount;
			var endAngle = Mathf.Tau * (segment + 1) / segmentCount;
			var start = center + Vector2.FromAngle(startAngle) * radius;
			var end = center + Vector2.FromAngle(endAngle) * radius;
			var midpoint = (start + end) * 0.5f;
			var inwardPoint = center + Vector2.FromAngle((startAngle + endAngle) * 0.5f) * (radius - TileSize * 0.5f);
			var midpointTile = new Vector2I(
				Mathf.FloorToInt(midpoint.X / TileSize),
				Mathf.FloorToInt(midpoint.Y / TileSize));
			var boundaryTile = new Vector2I(
				Mathf.FloorToInt(inwardPoint.X / TileSize),
				Mathf.FloorToInt(inwardPoint.Y / TileSize));

			// Relay circles only show the outward arc that contributes new range
			// beyond the coverage of the upstream building in the chain. Antennas
			// are network buildings too, so their extension is drawn in blue.
			if (!layer.IsBase && layer.UpstreamCoverage.Contains(midpointTile))
			{
				continue;
			}

			var alpha = CalculateAlpha(midpoint, observers, isSelected);
			DrawLine(start, end, WithAlpha(color, alpha * 0.18f), GlowLineWidth, true);
			DrawLine(start, end, WithAlpha(color, alpha), CoreLineWidth, true);

			if (!boundaryTiles.TryGetValue(boundaryTile, out var existingAlpha) || alpha > existingAlpha)
			{
				boundaryTiles[boundaryTile] = alpha;
			}
		}

		foreach (var entry in boundaryTiles)
		{
			var tilePosition = new Vector2(entry.Key.X * TileSize, entry.Key.Y * TileSize);
			DrawRect(
				new Rect2(tilePosition, new Vector2(TileSize, TileSize)),
				WithAlpha(color, entry.Value * BoundaryTileFillStrength),
				true);
		}
	}

	private float CalculateAlpha(Vector2 edgePosition, List<Vector2> observers, bool isSelected)
	{
		if (observers.Count == 0)
		{
			return isSelected ? SelectedMinimumAlpha : MinimumAlpha;
		}

		var nearestDistance = observers.Min(observer => observer.DistanceTo(edgePosition)) / TileSize;
		var proximity = 1.0f - Mathf.Clamp(nearestDistance / ProximityFadeDistanceInTiles, 0.0f, 1.0f);
		var smoothProximity = proximity * proximity * (3.0f - (2.0f * proximity));
		var alpha = Mathf.Lerp(MinimumAlpha, MaximumAlpha, smoothProximity);
		return isSelected ? Mathf.Max(alpha, SelectedMinimumAlpha) : alpha;
	}

	private static Vector2 GetBuildingCenterInWorld(BuildingComponent building)
	{
		var area = building.GetTileArea();
		return new Vector2(
			(area.Position.X + area.Size.X / 2.0f) * TileSize,
			(area.Position.Y + area.Size.Y / 2.0f) * TileSize);
	}

	private static Color WithAlpha(Color color, float alpha)
	{
		return new Color(color.R, color.G, color.B, Mathf.Clamp(alpha, 0.0f, 1.0f));
	}

	private void OnNetworkChanged(BuildingComponent _)
	{
		QueueRedraw();
	}

	private void OnBuildingDestroyed(BuildingComponent building)
	{
		if (building == selectedBuilding)
		{
			selectedBuilding = null;
		}
		QueueRedraw();
	}

	private void OnRobotSelected(BuildingComponent building)
	{
		selectedBuilding = building;
		QueueRedraw();
	}

	private void OnRobotSelectionCleared(BuildingComponent building)
	{
		if (building == selectedBuilding)
		{
			selectedBuilding = null;
			QueueRedraw();
		}
	}
}
