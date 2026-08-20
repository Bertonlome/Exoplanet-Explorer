using System;
using System.Collections.Generic;
using Game.Component;
using Game.Manager;
using Godot;

namespace Game.UI;

public partial class MinimapViewport : Control
{
	private const float TileSize = 64f;

	[Export] private Color backgroundColor = new(0.025f, 0.04f, 0.07f, 0.96f);
	[Export] private Color baseTerrainColor = new(0.20f, 0.34f, 0.23f, 1f);
	[Export] private Color elevatedTerrainColor = new(0.48f, 0.42f, 0.30f, 1f);
	[Export] private Color waterColor = new(0.08f, 0.25f, 0.48f, 1f);
	[Export] private Color baseMarkerColor = new(0.20f, 0.95f, 0.35f, 1f);
	[Export] private Color robotMarkerColor = new(0.95f, 0.20f, 0.20f, 1f);
	[Export] private Color selectedMarkerColor = new(1f, 0.85f, 0.15f, 1f);
	[Export] private Color cameraRectColor = new(0.20f, 0.95f, 1f, 1f);
	[Export(PropertyHint.Range, "1.05,2.0,0.05")]
	private float coarseZoomFactor = 1.35f;
	[Export(PropertyHint.Range, "0,12,1")]
	private float mapPadding = 6f;

	private readonly List<TerrainCell> terrainCache = new();
	private readonly List<EntityMarker> entityMarkers = new();
	private readonly Dictionary<Vector2I, FragmentBearingMarker> fragmentBearings = new();

	private Rect2I levelTileBounds;
	private Rect2 worldBounds;
	private GameCamera gameCamera;
	private GridManager gridManager;
	private BuildingManager buildingManager;
	private BuildingComponent selectedBuilding;
	private bool terrainCacheDirty = true;
	private bool isDirty;
	private bool isDragging;
	private bool inputEnabled = true;
	private Vector2 dragOffsetFromCameraCenter;
	private Vector2 lastCameraPosition;
	private Vector2 lastCameraZoom;
	private Vector2 lastControlSize;

	private readonly struct TerrainCell
	{
		public readonly Vector2I Tile;
		public readonly GridManager.MinimapTerrainType Type;

		public TerrainCell(Vector2I tile, GridManager.MinimapTerrainType type)
		{
			Tile = tile;
			Type = type;
		}
	}

	private readonly struct EntityMarker
	{
		public readonly Vector2 WorldPosition;
		public readonly Color Color;
		public readonly string Badge;
		public readonly bool IsBase;
		public readonly bool IsSelected;

		public EntityMarker(Vector2 worldPosition, Color color, string badge, bool isBase, bool isSelected)
		{
			WorldPosition = worldPosition;
			Color = color;
			Badge = badge;
			IsBase = isBase;
			IsSelected = isSelected;
		}
	}

	private readonly struct FragmentBearingMarker
	{
		public readonly Vector2 Direction;
		public readonly string CompassLabel;

		public FragmentBearingMarker(Vector2 direction, string compassLabel)
		{
			Direction = direction;
			CompassLabel = compassLabel ?? string.Empty;
		}
	}

	public void Initialize(
		Rect2I tileBounds,
		GameCamera camera,
		GridManager grid,
		BuildingManager buildings)
	{
		levelTileBounds = tileBounds;
		worldBounds = new Rect2(
			new Vector2(tileBounds.Position.X, tileBounds.Position.Y) * TileSize,
			new Vector2(tileBounds.Size.X, tileBounds.Size.Y) * TileSize);
		gameCamera = camera;
		gridManager = grid;
		buildingManager = buildings;
		terrainCacheDirty = true;

		RefreshMinimapData();
		CaptureCameraState();
	}

	public override void _Process(double delta)
	{
		if (gameCamera != null && IsInstanceValid(gameCamera) &&
			(!lastCameraPosition.IsEqualApprox(gameCamera.GlobalPosition) ||
			 !lastCameraZoom.IsEqualApprox(gameCamera.Zoom)))
		{
			CaptureCameraState();
			MarkDirty();
		}

		if (!lastControlSize.IsEqualApprox(Size))
		{
			lastControlSize = Size;
			MarkDirty();
		}
	}

	public void RefreshMinimapData()
	{
		if (gridManager == null || buildingManager == null || levelTileBounds.Size == Vector2I.Zero)
		{
			return;
		}

		if (terrainCacheDirty)
		{
			BuildTerrainCache();
			terrainCacheDirty = false;
		}

		BuildEntityMarkers();
		MarkDirty();
	}

	public void BuildTerrainCache()
	{
		terrainCache.Clear();
		if (gridManager == null) return;

		for (int y = levelTileBounds.Position.Y; y < levelTileBounds.End.Y; y++)
		{
			for (int x = levelTileBounds.Position.X; x < levelTileBounds.End.X; x++)
			{
				var tile = new Vector2I(x, y);
				var terrainType = gridManager.GetMinimapTerrainType(tile);
				if (terrainType != GridManager.MinimapTerrainType.None)
				{
					terrainCache.Add(new TerrainCell(tile, terrainType));
				}
			}
		}
	}

	public void BuildEntityMarkers()
	{
		entityMarkers.Clear();
		if (buildingManager == null) return;

		foreach (var building in BuildingComponent.GetValidBuildingComponents(buildingManager))
		{
			var resource = building.BuildingResource;
			if (resource == null) continue;

			bool isBase = resource.IsBase;
			bool isDrone = !isBase && resource.IsAerial;
			bool isRover = !isBase && string.Equals(resource.DisplayName, "Rover", StringComparison.OrdinalIgnoreCase);
			if (!isBase && !isDrone && !isRover) continue;

			var tileArea = building.GetTileArea();
			var markerWorldPosition = new Vector2(
				(tileArea.Position.X + tileArea.Size.X * 0.5f) * TileSize,
				(tileArea.Position.Y + tileArea.Size.Y * 0.5f) * TileSize);
			string badge = isDrone ? "D" : isRover ? "R" : string.Empty;
			entityMarkers.Add(new EntityMarker(
				markerWorldPosition,
				isBase ? baseMarkerColor : robotMarkerColor,
				badge,
				isBase,
				building == selectedBuilding));
		}
	}

	public override void _Draw()
	{
		isDirty = false;
		DrawRect(new Rect2(Vector2.Zero, Size), backgroundColor);

		Rect2 mapRect = GetMapDrawRect();
		if (mapRect.Size.X <= 0 || mapRect.Size.Y <= 0 || worldBounds.Size == Vector2.Zero)
		{
			return;
		}

		DrawRect(mapRect, new Color(0.04f, 0.07f, 0.09f, 1f));
		foreach (var cell in terrainCache)
		{
			Vector2 tileWorldPosition = new Vector2(cell.Tile.X, cell.Tile.Y) * TileSize;
			Vector2 cellStart = WorldToMinimap(tileWorldPosition);
			Vector2 cellEnd = WorldToMinimap(tileWorldPosition + Vector2.One * TileSize);
			DrawRect(new Rect2(cellStart, cellEnd - cellStart), GetTerrainColor(cell.Type));
		}

		foreach (var marker in entityMarkers)
		{
			DrawEntityMarker(marker);
		}
		DrawFragmentBearings(mapRect);

		Rect2 cameraRect = GetViewportRectOnMinimap();
		if (cameraRect.Size.X > 0 && cameraRect.Size.Y > 0)
		{
			DrawRect(cameraRect, new Color(0.01f, 0.02f, 0.03f, 0.8f), false, 3f);
			DrawRect(cameraRect, cameraRectColor, false, 1.5f);
		}

		DrawRect(mapRect, new Color(0.65f, 0.78f, 0.84f, 1f), false, 1.5f);
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!inputEnabled) return;
		if (gameCamera == null || !IsInstanceValid(gameCamera)) return;

		if (inputEvent is InputEventMouseButton mouseButton)
		{
			bool pointerIsOverMinimap = IsViewportPositionOverMinimap(mouseButton.Position);

			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (!pointerIsOverMinimap && !isDragging) return;

				Vector2 localPosition = ViewportToLocal(mouseButton.Position);
				isDragging = mouseButton.Pressed;
				if (mouseButton.Pressed)
				{
					gameCamera.CancelMouseDrag();

					Rect2 cameraRect = GetViewportRectOnMinimap();
					if (cameraRect.HasPoint(localPosition))
					{
						dragOffsetFromCameraCenter = localPosition - cameraRect.GetCenter();
					}
					else
					{
						dragOffsetFromCameraCenter = Vector2.Zero;
						MoveCameraRectangle(localPosition);
					}
				}
				else
				{
					dragOffsetFromCameraCenter = Vector2.Zero;
				}
				GetViewport().SetInputAsHandled();
				return;
			}

			if (pointerIsOverMinimap && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				gameCamera.CancelMouseDrag();
				gameCamera.SetCameraZoomStep(coarseZoomFactor);
				MarkDirty();
				GetViewport().SetInputAsHandled();
				return;
			}

			if (pointerIsOverMinimap && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				gameCamera.CancelMouseDrag();
				gameCamera.SetCameraZoomStep(1f / coarseZoomFactor);
				MarkDirty();
				GetViewport().SetInputAsHandled();
				return;
			}
		}

		if (inputEvent is InputEventMouseMotion mouseMotion && isDragging)
		{
			MoveCameraRectangle(ViewportToLocal(mouseMotion.Position));
			GetViewport().SetInputAsHandled();
		}
	}

	public void SetInputEnabled(bool enabled)
	{
		inputEnabled = enabled;
		SetProcessInput(enabled);
		if (enabled) return;

		isDragging = false;
		dragOffsetFromCameraCenter = Vector2.Zero;
		gameCamera?.CancelMouseDrag();
	}

	private bool IsViewportPositionOverMinimap(Vector2 viewportPosition)
	{
		return new Rect2(Vector2.Zero, Size).HasPoint(ViewportToLocal(viewportPosition));
	}

	private Vector2 ViewportToLocal(Vector2 viewportPosition)
	{
		return GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
	}

	public Vector2 WorldToMinimap(Vector2 worldPosition)
	{
		Rect2 mapRect = GetMapDrawRect();
		if (worldBounds.Size.X <= 0 || worldBounds.Size.Y <= 0) return mapRect.Position;

		Vector2 normalized = new Vector2(
			(worldPosition.X - worldBounds.Position.X) / worldBounds.Size.X,
			(worldPosition.Y - worldBounds.Position.Y) / worldBounds.Size.Y);
		return mapRect.Position + normalized * mapRect.Size;
	}

	public Vector2 MinimapToWorld(Vector2 localPosition)
	{
		Rect2 mapRect = GetMapDrawRect();
		if (mapRect.Size.X <= 0 || mapRect.Size.Y <= 0) return worldBounds.GetCenter();

		Vector2 normalized = new Vector2(
			Mathf.Clamp((localPosition.X - mapRect.Position.X) / mapRect.Size.X, 0f, 1f),
			Mathf.Clamp((localPosition.Y - mapRect.Position.Y) / mapRect.Size.Y, 0f, 1f));
		return worldBounds.Position + normalized * worldBounds.Size;
	}

	public Rect2 GetViewportRectOnMinimap()
	{
		if (gameCamera == null || !IsInstanceValid(gameCamera)) return new Rect2();

		Rect2 visibleWorldRect = gameCamera.GetVisibleWorldRect();
		Vector2 start = WorldToMinimap(visibleWorldRect.Position);
		Vector2 end = WorldToMinimap(visibleWorldRect.End);
		Rect2 mapRect = GetMapDrawRect();
		start = new Vector2(
			Mathf.Clamp(start.X, mapRect.Position.X, mapRect.End.X),
			Mathf.Clamp(start.Y, mapRect.Position.Y, mapRect.End.Y));
		end = new Vector2(
			Mathf.Clamp(end.X, mapRect.Position.X, mapRect.End.X),
			Mathf.Clamp(end.Y, mapRect.Position.Y, mapRect.End.Y));
		return new Rect2(start, end - start);
	}

	public void SetCameraZoomStep(float factor)
	{
		gameCamera?.SetCameraZoomStep(factor);
		MarkDirty();
	}

	public void SetSelectedBuilding(BuildingComponent building)
	{
		selectedBuilding = building;
		BuildEntityMarkers();
		MarkDirty();
	}

	public void SetFragmentBearing(
		Vector2I fragmentPosition,
		Vector2? direction,
		string compassLabel = null)
	{
		if (!direction.HasValue || direction.Value.LengthSquared() <= 0.0001f)
			fragmentBearings.Remove(fragmentPosition);
		else
			fragmentBearings[fragmentPosition] = new FragmentBearingMarker(
				direction.Value.Normalized(), compassLabel);
		MarkDirty();
	}

	public void MarkTerrainDirty()
	{
		terrainCacheDirty = true;
		RefreshMinimapData();
	}

	public void MarkDirty()
	{
		if (isDirty) return;
		isDirty = true;
		QueueRedraw();
	}

	private Rect2 GetMapDrawRect()
	{
		Vector2 availableSize = new Vector2(
			MathF.Max(Size.X - mapPadding * 2f, 0f),
			MathF.Max(Size.Y - mapPadding * 2f, 0f));
		if (availableSize.X <= 0 || availableSize.Y <= 0 || worldBounds.Size.X <= 0 || worldBounds.Size.Y <= 0)
		{
			return new Rect2(Vector2.One * mapPadding, Vector2.Zero);
		}

		float scale = MathF.Min(
			availableSize.X / worldBounds.Size.X,
			availableSize.Y / worldBounds.Size.Y);
		Vector2 drawSize = worldBounds.Size * scale;
		return new Rect2((Size - drawSize) * 0.5f, drawSize);
	}

	private Color GetTerrainColor(GridManager.MinimapTerrainType terrainType)
	{
		return terrainType switch
		{
			GridManager.MinimapTerrainType.Water => waterColor,
			GridManager.MinimapTerrainType.Elevated => elevatedTerrainColor,
			_ => baseTerrainColor
		};
	}

	private void DrawEntityMarker(EntityMarker marker)
	{
		Vector2 center = WorldToMinimap(marker.WorldPosition);
		float size = marker.IsBase ? 11f : 12f;
		var markerRect = new Rect2(center - Vector2.One * size * 0.5f, Vector2.One * size);
		DrawRect(markerRect, marker.Color);
		DrawRect(markerRect, marker.IsSelected ? selectedMarkerColor : Colors.Black, false, marker.IsSelected ? 2f : 1f);

		if (!string.IsNullOrEmpty(marker.Badge))
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(markerRect.Position.X, markerRect.Position.Y + size * 0.82f),
				marker.Badge,
				HorizontalAlignment.Center,
				size,
				10,
				Colors.White);
		}
	}

	private void DrawFragmentBearings(Rect2 mapRect)
	{
		Color rayColor = new(0.1f, 0.95f, 1f, 0.96f);
		foreach ((Vector2I tile, FragmentBearingMarker bearing) in fragmentBearings)
		{
			Vector2 originWorld = (new Vector2(tile.X, tile.Y) + Vector2.One * 0.5f) * TileSize;
			Vector2 start = WorldToMinimap(originWorld);
			Vector2 oneTile = WorldToMinimap(originWorld + bearing.Direction * TileSize) - start;
			if (oneTile.LengthSquared() <= 0.0001f || !mapRect.HasPoint(start)) continue;
			Vector2 ray = oneTile.Normalized();
			float length = DistanceToRectEdge(start, ray, mapRect);
			Vector2 end = start + ray * MathF.Max(length - 3f, 0f);
			DrawLine(start, end, Colors.Black, 6f, true);
			DrawLine(start, end, rayColor, 3f, true);
			DrawArrowHead(end, ray, rayColor);
			DrawCircle(start, 5f, Colors.Black, true);
			DrawCircle(start, 3.5f, rayColor, true);
			string label = string.IsNullOrEmpty(bearing.CompassLabel)
				? "FRAGMENT BEARING"
				: $"FRAGMENT · {bearing.CompassLabel}";
			Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(
				label, HorizontalAlignment.Left, -1, 10);
			Vector2 labelPosition = new(
				Mathf.Clamp(start.X + 6f, mapRect.Position.X + 2f,
					mapRect.End.X - textSize.X - 2f),
				Mathf.Clamp(start.Y - 7f, mapRect.Position.Y + textSize.Y,
					mapRect.End.Y - 2f));
			DrawString(ThemeDB.FallbackFont, labelPosition, label,
				HorizontalAlignment.Left, -1, 10, rayColor);
		}
	}

	private static float DistanceToRectEdge(Vector2 start, Vector2 direction, Rect2 rectangle)
	{
		float distance = float.PositiveInfinity;
		if (direction.X > 0.0001f)
			distance = MathF.Min(distance, (rectangle.End.X - start.X) / direction.X);
		else if (direction.X < -0.0001f)
			distance = MathF.Min(distance, (rectangle.Position.X - start.X) / direction.X);
		if (direction.Y > 0.0001f)
			distance = MathF.Min(distance, (rectangle.End.Y - start.Y) / direction.Y);
		else if (direction.Y < -0.0001f)
			distance = MathF.Min(distance, (rectangle.Position.Y - start.Y) / direction.Y);
		return float.IsInfinity(distance) ? 0f : MathF.Max(distance, 0f);
	}

	private void DrawArrowHead(Vector2 tip, Vector2 direction, Color color)
	{
		Vector2 back = -direction.Normalized();
		DrawLine(tip, tip + back.Rotated(0.58f) * 9f, Colors.Black, 5f);
		DrawLine(tip, tip + back.Rotated(-0.58f) * 9f, Colors.Black, 5f);
		DrawLine(tip, tip + back.Rotated(0.58f) * 9f, color, 2.5f);
		DrawLine(tip, tip + back.Rotated(-0.58f) * 9f, color, 2.5f);
	}

	private void MoveCameraRectangle(Vector2 localPosition)
	{
		Vector2 desiredCenter = localPosition - dragOffsetFromCameraCenter;
		gameCamera.CenterOnPositionClamped(MinimapToWorld(desiredCenter));
		CaptureCameraState();
		MarkDirty();
	}

	private void CaptureCameraState()
	{
		if (gameCamera == null || !IsInstanceValid(gameCamera)) return;
		lastCameraPosition = gameCamera.GlobalPosition;
		lastCameraZoom = gameCamera.Zoom;
	}
}
