using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Game.Autoload;
using Game.Building;
using Game.Component;
using Game.Level.Util;
using Godot;

namespace Game.Manager;

public partial class GridManager : Node
{
	public enum ResourceType
	{
		Wood,
		RedMineral,
		GreenMineral,
		BlueMineral,
		None
	}

	public enum MinimapTerrainType
	{
		None,
		Base,
		Elevated,
		Water
	}
	private const string IS_BUILDABLE = "is_buildable";
	private const string IS_WOOD = "is_wood";
	private const string IS_MINERAL = "is_mineral";
	private const string IS_IGNORED = "is_ignored";
	private const string IS_ROUGH_TERRAIN = "is_rough_terrain";
	private const string WOOD = "wood";
	private const string IS_MUD = "is_mud";
	private const string IS_BRIDGE = "is_bridge";
	public const string IS_WATER = "is_water";

	[Signal]
	public delegate void ResourceTilesUpdatedEventHandler(Vector2I tile, int collectedTiles, string resourceType);
	[Signal]
	public delegate void MineralTilesUpdatedEventHandler(Vector2I tile, int collectedTiles, string mineralType);
	[Signal]
	public delegate void DiscoveredTileUpdatedEventHandler(Vector2I tile, string type);
	[Signal]
	public delegate void GridStateUpdatedEventHandler();
	[Signal]
	public delegate void GroundRobotTouchingMonolithEventHandler();
	[Signal]
	public delegate void BaseTouchingMonolithEventHandler();
	[Signal]
	public delegate void AerialRobotHasVisionOfMonolithEventHandler();

	private HashSet<Vector2I> allTilesBuildableOnTheMap = new();
	private HashSet<Vector2I> validBuildableTiles = new();
	private HashSet<Vector2I> validBuildableAttackTiles = new();
	private HashSet<Vector2I> allTilesInBuildingRadius = new();
	private HashSet<Vector2I> collectedResourceTiles = new();
	private HashSet<Vector2I> collectedMineralTiles = new();
	private HashSet<Vector2I> discoveredElementsTiles = new();
	private HashSet<Vector2I> occupiedTiles = new();
	private HashSet<Vector2I> dangerOccupiedTiles = new();
	public HashSet<Vector2I> baseAntennaCoveredTiles = new();
	private HashSet<Vector2I> baseProximityTiles = new();
	private HashSet<Vector2I> monolithTiles = new();
	private HashSet<Vector2I> monolithFragmentTiles = new();
	private bool buildableTileCacheDirty = true;
	private HashSet<Vector2I> connectedNetworkCoverageTiles = new();
	private HashSet<BuildingComponent> connectedNetworkBuildings = new();
	private bool movementCoverageCacheInitialized = false;

	public Rect2I baseArea = new();

	private List<MonolithFragment> fragments = new();

	private Monolith monolith;
	public Vector2I monolithPosition = new();

	private List<Vector2I> allTilesBaseLayer;

	[Export]
	private TileMapLayer highlightTilemapLayer;
	[Export]
	private TileMapLayer baseTerrainTilemapLayer;
	[Export]
	private bool showHighlightedTiles = false;
	[Export]
	private TileMapLayer bridgeTileMapLayerBase;
	[Export]
	private TileMapLayer bridgeTileMapLayerElevation;
	[Export]
	private GravitationalAnomalyMap gravitationalAnomalyMap;

	private List<TileMapLayer> allTilemapLayers = new();
	private Dictionary<TileMapLayer, ElevationLayer> tileMapLayerToElevationLayer = new();
	private Dictionary<BuildingComponent, HashSet<Vector2I>> buildingToBuildableTiles = new();
	private Dictionary<Vector2I, BuildingComponent> TileToBuilding = new();
	private Dictionary<BuildingComponent, HashSet<Vector2I>> buildingStuckToTiles = new();

	public override void _Ready()
	{
		ClearAll();
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingPlaced, Callable.From<BuildingComponent>(OnBuildingPlaced));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingDestroyed, Callable.From<BuildingComponent>(OnBuildingDestroyed));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingEnabled, Callable.From<BuildingComponent>(OnBuildingEnabled));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingDisabled, Callable.From<BuildingComponent>(OnBuildingDisabled));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingMoved, Callable.From<BuildingComponent>(OnBuildingMoved));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingStuck, Callable.From<BuildingComponent>(OnBuildingStuck));
		GameEvents.Instance.Connect(GameEvents.SignalName.BuildingUnStuck, Callable.From<BuildingComponent>(OnBuildingUnStuck));
		GameEvents.Instance.Connect(GameEvents.SignalName.RobotSelected, Callable.From<BuildingComponent>(OnRobotSelected));

		monolith = GetNode<Monolith>("%Monolith");
		SetMonolithPosition(ConvertWorldPositionToTilePosition(monolith.GlobalPosition));

		fragments = GetParent().GetNodesOfType<MonolithFragment>();
		SetMonolithFragmentsPosition(fragments.Select(fragment => ConvertWorldPositionToTilePosition(fragment.GlobalPosition)).ToList());

		allTilemapLayers = GetAllTilemapLayers(baseTerrainTilemapLayer);
		allTilesBuildableOnTheMap = GetAllBuildableBaseTerrainTiles(baseTerrainTilemapLayer).ToHashSet();
		allTilesBaseLayer = baseTerrainTilemapLayer.GetUsedCells().ToList();
		MapTileMapLayersToElevationLayers();
	}

	public void SetBaseArea(Vector2I dimensions, Vector2I position)
	{
		baseArea = new Rect2I(position, dimensions);
	}

	private void OnRobotSelected(BuildingComponent buildingComponent)
	{
		if (buildingComponent.BuildingResource.IsAerial)
		{
			CallDeferred(nameof(CheckGroundRobotBelow), buildingComponent);
		}
	}

	public void SetMonolithFragmentsPosition(List<Vector2I> positions)
	{
		monolithFragmentTiles = positions.ToHashSet();
	}
	public void SetMonolithPosition(Vector2I position)
	{
		monolithPosition = position;
		monolithTiles.Add(position);
		//SetGravitationAnomalyGradient(position);
	}

	public (TileMapLayer, bool) GetTileCustomData(Vector2I tilePosition, string dataName)
	{
		foreach (var layer in allTilemapLayers)
		{
			var customData = layer.GetCellTileData(tilePosition);
			if (customData == null || (bool)customData.GetCustomData(IS_IGNORED)) continue;

			var value = (bool)customData.GetCustomData(dataName);
			// The TileSet still identifies a harvested tree tile as wood, but at
			// runtime that tree has become a trunk and no longer blocks movement.
			if (dataName == IS_WOOD && collectedResourceTiles.Contains(tilePosition))
			{
				value = false;
			}

			return (layer, value);
		}
		return (null, false);
	}

	/// <summary>
	/// Returns terrain data used by ground movement, ignoring a tree tile that is
	/// drawn over the actual terrain. This is especially important on elevated
	/// terrain: some levels keep their TreeLayer outside the ElevationLayer, so
	/// using the tree layer for elevation checks incorrectly makes the tile look
	/// like base terrain. Harvested trees remain present in the TileMap and are
	/// represented visually as trunks, so the raw is_wood flag is used here.
	/// </summary>
	private (TileMapLayer, bool) GetGroundTerrainCustomData(Vector2I tilePosition, string dataName)
	{
		foreach (var layer in allTilemapLayers)
		{
			var customData = layer.GetCellTileData(tilePosition);
			if (customData == null || (bool)customData.GetCustomData(IS_IGNORED))
			{
				continue;
			}

			if ((bool)customData.GetCustomData(IS_WOOD))
			{
				continue;
			}

			return (layer, (bool)customData.GetCustomData(dataName));
		}

		return (null, false);
	}

	public (ElevationLayer elevationLayer, bool isElevated) GetElevationLayerForTile(Vector2I tilePosition)
	{
		var (terrainLayer, _) = GetGroundTerrainCustomData(tilePosition, IS_BUILDABLE);
		if (terrainLayer != null)
		{
			var elevationLayer = tileMapLayerToElevationLayer.GetValueOrDefault(terrainLayer);
			bool isElevated = elevationLayer != null && elevationLayer.Name.ToString().StartsWith("ElevationLayer", StringComparison.Ordinal);
			return (elevationLayer, isElevated);
		}
		
		// Fallback: if no valid tile found, use baseTerrainTilemapLayer's elevation
		var fallbackElevationLayer = tileMapLayerToElevationLayer.GetValueOrDefault(baseTerrainTilemapLayer);
		bool fallbackIsElevated = fallbackElevationLayer != null && fallbackElevationLayer.Name.ToString().StartsWith("ElevationLayer", StringComparison.Ordinal);
		return (fallbackElevationLayer, fallbackIsElevated);
	}

	public MinimapTerrainType GetMinimapTerrainType(Vector2I tilePosition)
	{
		var (tileMapLayer, isWater) = GetTileCustomData(tilePosition, IS_WATER);
		if (tileMapLayer == null)
		{
			return MinimapTerrainType.None;
		}

		if (isWater)
		{
			return MinimapTerrainType.Water;
		}

		var (_, isElevated) = GetElevationLayerForTile(tilePosition);
		return isElevated ? MinimapTerrainType.Elevated : MinimapTerrainType.Base;
	}

	public bool TryPlaceBridgeTile(Rect2I robotPosition, Rect2I bridgeArea, string orientation)
	{
		using (Telemetry.Scope("GridManager.TryPlaceBridgeTile"))
		{
		var (robotElevation, robotIsElevated) = GetElevationLayerForTile(robotPosition.Position);
		var (targetElevation, targetIsElevated) = GetElevationLayerForTile(bridgeArea.Position);
		var bridgeTileMapLayer = bridgeTileMapLayerBase;
		
		if (robotElevation == null && targetElevation != null)
		{
			GD.PrintErr("Cannot place bridge tile: Robot elevation layer and target elevation layer do not match.");
			return false;
		}
		if (robotIsElevated)
		{
			bridgeTileMapLayer = bridgeTileMapLayerElevation;
		}
		else
		{
			bridgeTileMapLayer = bridgeTileMapLayerBase;
		}
		if (orientation == "horizontal")
		{
			var position = bridgeArea.Position;
			bridgeTileMapLayer.SetCell(position, 14, new Vector2I(1, 0)); // Assuming 14 is the bridge tile ID
		}
		else if (orientation == "vertical")
		{
			var position = bridgeArea.Position;
			bridgeTileMapLayer.SetCell(position, 14, new Vector2I(0, 2)); // Assuming 14 is the bridge tile ID
		}
		return true;
		}
	}

	public string GetTileDiscoveredElements(Vector2I tilePosition)
	{
		foreach (var layer in allTilemapLayers)
		{
			var customData = layer.GetCellTileData(tilePosition);
			if (customData == null || (bool)customData.GetCustomData(IS_IGNORED)) continue;
			return (string)customData.GetCustomData("landscape_type");
		}
		return null;
	}

	public bool IsTilePositionInAnyBuildingRadius(Vector2I tilePosition)
	{
		EnsureBuildableTileCache();
		return allTilesInBuildingRadius.Contains(tilePosition);
	}

	public bool IsTileAreaBuildable(Rect2I tileArea, bool isAttackTiles = false, bool isBase = false, bool isBridge = false)
	{
		EnsureBuildableTileCache();
		IEnumerable<Vector2I> tileSetToCheck;
		var tiles = tileArea.ToTiles();
		if (tiles.Count == 0) return false;

		(TileMapLayer firstTileMapLayer, _) = GetTileCustomData(tiles[0], IS_BUILDABLE);
		var targetElevationLayer = firstTileMapLayer != null ? tileMapLayerToElevationLayer[firstTileMapLayer] : null;

		if (BuildingManager.selectedBuildingComponent != null)
		{
			tileSetToCheck = GetBuildableTileSet(isAttackTiles).Except(BuildingManager.selectedBuildingComponent.GetOccupiedCellPositions());
		}
		if (isBase)
		{
			tileSetToCheck = allTilesBuildableOnTheMap;
		}
		else if (isBridge)
		{
			return true;
		}
		else
		{
			tileSetToCheck = GetBuildableTileSet(isAttackTiles);
		}
		if (isAttackTiles)
		{
			tileSetToCheck = tileSetToCheck.Except(occupiedTiles).ToHashSet();
		}

		return tiles.All((tilePosition) =>
		{
			(TileMapLayer tileMapLayer, bool isBuildable) = GetTileCustomData(tilePosition, IS_BUILDABLE);
			var elevationLayer = tileMapLayer != null ? tileMapLayerToElevationLayer[tileMapLayer] : null;
			return isBuildable && tileSetToCheck.Contains(tilePosition) && elevationLayer == targetElevationLayer;
		});
	}

	public bool IsTileOccupied(Vector2I tilePosition)
	{
		return occupiedTiles.Contains(tilePosition);
	}

	public bool IsBuildingMovable(BuildingComponent buildingComponent, Rect2I originArea, Rect2I destinationArea, bool considerBridge = false, bool? bridgeElevationIsElevated = null)
	{
		var tilesDestination = destinationArea.ToTiles();
		var tilesOrigin = originArea.ToTiles();

		if (tilesDestination.Count == 0) return false;
		var originTiles = tilesOrigin.ToHashSet();
		var originTile = tilesOrigin[0];

		if (buildingComponent.BuildingResource.IsAerial)
		{
			return tilesDestination.All((tilePosition) =>
			{
				// Aerial units must stay on valid map tiles; otherwise A* can expand forever off-map.
				(TileMapLayer tileMapLayer, _) = GetTileCustomData(tilePosition, IS_BUILDABLE);
				if (tileMapLayer == null)
				{
					return false;
				}

				if (occupiedTiles.Contains(tilePosition) && !originTiles.Contains(tilePosition))
				{
					return false;
				}

				(_, bool isWood) = GetTileCustomData(tilePosition, IS_WOOD);
				return !isWood;
			});
		}

		(TileMapLayer firstTileMapLayer, _) = GetGroundTerrainCustomData(tilesDestination[0], IS_ROUGH_TERRAIN);
		var targetElevationLayer = firstTileMapLayer != null ? tileMapLayerToElevationLayer[firstTileMapLayer] : null;

		(firstTileMapLayer, _) = GetGroundTerrainCustomData(originTile, IS_ROUGH_TERRAIN);
		var OriginElevationLayer = firstTileMapLayer != null ? tileMapLayerToElevationLayer[firstTileMapLayer] : null;
		var (robotElevation, robotIsElevated) = GetElevationLayerForTile(originTile);
		(_, bool isInMud) = GetGroundTerrainCustomData(originTile, IS_MUD);

		return tilesDestination.All((tilePosition) =>
		{
			(TileMapLayer roughLayer, bool isRoulable) = GetGroundTerrainCustomData(tilePosition, IS_ROUGH_TERRAIN);
			var elevationLayer = roughLayer != null ? tileMapLayerToElevationLayer[roughLayer] : null;
			(TileMapLayer mudLayer, bool isMud) = GetGroundTerrainCustomData(tilePosition, IS_MUD);
			(TileMapLayer waterLayer, bool isWater) = GetGroundTerrainCustomData(tilePosition, IS_WATER);
			var (targetElevation, targetIsElevated) = GetElevationLayerForTile(tilePosition);

			// DEBUG: Log tile checking details
			//GD.Print($"[Bridge Check] Tile {tilePosition}:");
			//GD.Print($"  - elevationLayer: {elevationLayer?.Name ?? "null"}");
			//GD.Print($"  - targetElevationLayer: {targetElevationLayer?.Name ?? "null"}");
			//GD.Print($"  - roughLayer: {roughLayer?.Name ?? "null"}, woodLayer: {woodLayer?.Name ?? "null"}, mudLayer: {mudLayer?.Name ?? "null"}, waterLayer: {waterLayer?.Name ?? "null"}");
			//GD.Print($"  - originElevation: {robotElevation?.Name ?? "null"}, tileElevation: {targetElevation?.Name ?? "null"}");
			//GD.Print($"  - robotIsElevated: {robotIsElevated}, targetIsElevated: {targetIsElevated}");
			//GD.Print($"  - isWater: {isWater}, isRoulable: {isRoulable}, isWood: {isWood}");
			//GD.Print($"  - considerBridge: {considerBridge}, bridgeElevationIsElevated: {(bridgeElevationIsElevated.HasValue ? bridgeElevationIsElevated.Value.ToString() : "null")}");
			// End of DEBUG

			// When considerBridge is true and bridgeElevationIsElevated is set,
			// we're planning a bridge at a specific elevation level
			// KEY MECHANIC: Elevated bridges go OVER base terrain (spanning cliff to cliff)
			bool canCrossWithBridge = false;
			if (considerBridge && bridgeElevationIsElevated.HasValue)
			{
				if (bridgeElevationIsElevated.Value)
				{
					// ELEVATED BRIDGE allows:
					// 1. Crossing over base terrain (bridge spans over it)
					// 2. Crossing elevated water
					// 3. Reaching elevated land (destination cliff)
					canCrossWithBridge = !targetIsElevated || (targetIsElevated && (isWater || OriginElevationLayer != targetElevationLayer));
				}
				else
				{
					// BASE BRIDGE: Can only cross base water at base elevation
					if (isWater && !targetIsElevated)
					{
						canCrossWithBridge = true;
					}
				}
			}

			var check1 = !occupiedTiles.Contains(tilePosition) || originTiles.Contains(tilePosition);
			var check2 = elevationLayer == targetElevationLayer || canCrossWithBridge;
			var check3 = OriginElevationLayer == targetElevationLayer || canCrossWithBridge || 
			             (isMud && OriginElevationLayer == targetElevationLayer) || 
			             (isInMud && OriginElevationLayer == targetElevationLayer);
			var check7 = !isRoulable;
			var check9 = !isWater || canCrossWithBridge;

			//GD.Print($"  - check1 (not occupied or origin tile): {check1}");
			//GD.Print($"  - check2 (same elevation as target or bridge-cross): {check2}");
			//GD.Print($"  - check3 (origin elevation compatible or bridge-cross): {check3}");
			//GD.Print($"  - check7 (not rough terrain): {check7}");
			//GD.Print($"  - check9 (not water or bridge-cross): {check9}");

			var canMoveOnThisTile = check1 && check2 && check3 && check7 && check9;
			if (!canMoveOnThisTile)
			{
				var failedChecks = new List<string>();
				if (!check1) failedChecks.Add("check1");
				if (!check2) failedChecks.Add("check2");
				if (!check3) failedChecks.Add("check3");
				if (!check7) failedChecks.Add("check7");
				if (!check9) failedChecks.Add("check9");
				//GD.Print($"  - FAILED checks: {string.Join(", ", failedChecks)}");
			}
			//GD.Print($"  - tileResult: {canMoveOnThisTile}");

			return canMoveOnThisTile;
		});
	}

	public bool IsGettingOutOfACoverage(BuildingComponent buildingComponent, Rect2I destinationArea)
	{
		var tilesInRadiusofRobotArrival = GetValidTilesInRadius(destinationArea, buildingComponent.BuildingResource.BuildableRadius);
		
		buildingToBuildableTiles.Remove(buildingComponent);

		var allTilesFromDictionary = new HashSet<Vector2I>();
		foreach(var tileSet in buildingToBuildableTiles.Values)
		{
			allTilesFromDictionary.UnionWith(tileSet);
		}

		var anyTilesInRadius = tilesInRadiusofRobotArrival		
			.Any((tilePosition) => 
			{
				return allTilesFromDictionary.Contains(tilePosition);
			});
	
		bool testfinal = (tilesInRadiusofRobotArrival.Intersect(baseAntennaCoveredTiles).Count() > 0) || anyTilesInRadius == true;

		if((tilesInRadiusofRobotArrival.Intersect(baseAntennaCoveredTiles).Count() > 0) || anyTilesInRadius) return true;
		else return true;
	}

	public void HighlightDangerOccupiedTiles()
	{
		using (Telemetry.Scope("GridManager.HighlightDangerOccupiedTiles"))
		{
			var atlasCoords = new Vector2I(2, 0);
			foreach (var tilePosition in dangerOccupiedTiles)
			{
				highlightTilemapLayer.SetCell(tilePosition, 0, atlasCoords);
			}
		}
	}

	public void HighlightBuildableTiles(bool isAttackTiles = false)
	{
		using (Telemetry.Scope("GridManager.HighlightBuildableTiles"))
		{
			EnsureBuildableTileCache();
			if (!showHighlightedTiles) return;
			foreach (var tilePosition in GetValidTileSet())
			{
				highlightTilemapLayer.SetCell(tilePosition, 0, Vector2I.Zero);
			}
		}
	}

	public void HighlightBridgePlaceableTiles(Rect2I robotPosition)
	{
		highlightTilemapLayer.SetCell(new Vector2I(robotPosition.Position.X -1, robotPosition.Position.Y), 0, new Vector2I(1, 0));
		highlightTilemapLayer.SetCell(new Vector2I(robotPosition.Position.X + 1, robotPosition.Position.Y), 0, new Vector2I(1, 0));
		highlightTilemapLayer.SetCell(new Vector2I(robotPosition.Position.X, robotPosition.Position.Y -1), 0, new Vector2I(1, 0));
		highlightTilemapLayer.SetCell(new Vector2I(robotPosition.Position.X, robotPosition.Position.Y + 1), 0, new Vector2I(1, 0));
	}

	public void HighlightExpandedBuildableTiles(Rect2I tileArea, int radius)
	{
		using (Telemetry.Scope("GridManager.HighlightExpandedBuildableTiles"))
		{
			EnsureBuildableTileCache();
			var validTiles = GetValidTilesInRadius(tileArea, radius).ToHashSet();
			var expandedTiles = validTiles.Except(validBuildableTiles).Except(occupiedTiles);
			var atlasCoords = new Vector2I(1, 0);
			foreach (var tilePosition in expandedTiles)
			{
				highlightTilemapLayer.SetCell(tilePosition, 0, atlasCoords);
			}
		}
	}

	public void HighlightAttackTiles(Rect2I tileArea, int radius)
	{
		using (Telemetry.Scope("GridManager.HighlightAttackTiles"))
		{
			EnsureBuildableTileCache();
			var buildingAreaTiles = tileArea.ToTiles();
			var validTiles = GetValidTilesInRadius(tileArea, radius).ToHashSet()
				.Except(validBuildableAttackTiles)
				.Except(buildingAreaTiles);

			var atlasCoords = new Vector2I(1, 0);
			foreach (var tilePosition in validTiles)
			{
				highlightTilemapLayer.SetCell(tilePosition, 0, atlasCoords);
			}
		}
	}

	public void HighlightResourceTiles(Rect2I tileArea, int radius)
	{
		using (Telemetry.Scope("GridManager.HighlightResourceTiles"))
		{
			var resourceTiles = GetWoodTilesInRadius(tileArea, radius);
			var atlasCoords = new Vector2I(1, 0);
			foreach (var tilePosition in resourceTiles)
			{
				highlightTilemapLayer.SetCell(tilePosition, 0, atlasCoords);
			}
		}
	}

	public void ClearHighlightedTiles()
	{
		using (Telemetry.Scope("GridManager.ClearHighlightedTiles"))
		{
			highlightTilemapLayer.Clear();
		}
	}

	public Vector2I GetMouseGridCellPositionWithDimensionOffset(Vector2 dimensions)
	{
		var mouseGridPosition = highlightTilemapLayer.GetGlobalMousePosition() / 64;
		mouseGridPosition -= dimensions / 2;
		mouseGridPosition = mouseGridPosition.Round();
		return new Vector2I((int)mouseGridPosition.X, (int)mouseGridPosition.Y);
	}

	public Vector2I GetMouseGridCellPosition()
	{
		var mousePosition = highlightTilemapLayer.GetGlobalMousePosition();
		return ConvertWorldPositionToTilePosition(mousePosition);
	}

	public Vector2I ConvertWorldPositionToTilePosition(Vector2 worldPosition)
	{
		var tilePosition = worldPosition / 64;
		tilePosition = tilePosition.Floor();
		return new Vector2I((int)tilePosition.X, (int)tilePosition.Y);
	}

	public bool CanMoveBuilding(BuildingComponent toMoveBuildingComponent, Rect2I destinationArea = new Rect2I())
	{
		EnsureMovementCoverageCache();

		if(destinationArea.Area == 0)
		{
			destinationArea = toMoveBuildingComponent.GetAreaOccupied(ConvertWorldPositionToTilePosition(toMoveBuildingComponent.GlobalPosition));
		}
		
		var tilesInRadiusofRobotArrival = GetValidTilesInRadius(destinationArea, toMoveBuildingComponent.BuildingResource.BuildableRadius);

		if(toMoveBuildingComponent.BuildingResource.BuildableRadius > 0)
		{
			buildingToBuildableTiles.TryGetValue(toMoveBuildingComponent, out var currentCoverage);
			foreach (var tilePosition in tilesInRadiusofRobotArrival)
			{
				if (baseAntennaCoveredTiles.Contains(tilePosition))
				{
					return true;
				}

				if (connectedNetworkCoverageTiles.Contains(tilePosition) && (currentCoverage == null || !currentCoverage.Contains(tilePosition)))
				{
					return true;
				}
			}

			return false;
		}
		return false;
	}

	public bool CanDestroyBuilding(BuildingComponent toDestroyBuildingComponent)
	{
		EnsureBuildableTileCache();
		if (toDestroyBuildingComponent.BuildingResource.BuildableRadius > 0)
		{
			return !WillBuildingDestructionCreateOrphanBuildings(toDestroyBuildingComponent) &&
				IsBuildingNetworkConnected(toDestroyBuildingComponent);
		}
		return true;
	}

	public HashSet<Vector2I> GetCollectedResourcetiles()
	{
		return collectedResourceTiles.Union(collectedMineralTiles).ToHashSet();
	}

	public HashSet<Vector2I> GetDiscoveredResourceTiles()
	{
		return discoveredElementsTiles.ToHashSet();
	}

	public bool IsInBaseProximity(Vector2I position)
	{
		return baseProximityTiles.Contains(position);
	}

	private bool WillBuildingDestructionCreateOrphanBuildings(BuildingComponent toDestroyBuildingComponent)
	{
		var dependentBuildings = BuildingComponent.GetValidBuildingComponents(this)
			.Where((buildingComponent) =>
			{
				if (buildingComponent == toDestroyBuildingComponent) return false;
				if (buildingComponent.BuildingResource.IsBase) return false;

				var anyTilesInRadius = buildingComponent.GetOccupiedCellPositions()
					.Any((tilePosition) => buildingToBuildableTiles[toDestroyBuildingComponent].Contains(tilePosition));
				return anyTilesInRadius;
			});

		var allBuildingsStillValid = dependentBuildings.All((dependentBuilding) =>
		{
			var tilesForBuilding = dependentBuilding.GetOccupiedCellPositions();
			var buildingsToCheck = buildingToBuildableTiles.Keys
				.Where((key) => key != toDestroyBuildingComponent && key != dependentBuilding);

			return tilesForBuilding.All((tilePosition) =>
			{
				var tileIsInSet = buildingsToCheck
					.Any((buildingComponent) => buildingToBuildableTiles[buildingComponent].Contains(tilePosition));
				return tileIsInSet;
			});
		});

		if (!allBuildingsStillValid)
		{
			return true;
		}

		return false;
	}

	private bool IsBuildingNetworkConnected(BuildingComponent toMoveBuildingComponent)
	{
		var baseBuilding = BuildingComponent.GetValidBuildingComponents(this)
			.First((buildingComponent) => buildingComponent.BuildingResource.IsBase);

		var visitedBuildings = new HashSet<BuildingComponent>();
		VisitAllConnectedBuildings(baseBuilding, toMoveBuildingComponent, visitedBuildings);

		var totalBuildingsToVisit = BuildingComponent.GetValidBuildingComponents(this)
			.Count((buildingComponent) =>
			{
				return buildingComponent != toMoveBuildingComponent && buildingComponent.BuildingResource.BuildableRadius > 0;
			});

		return totalBuildingsToVisit == visitedBuildings.Count;
	}

	private bool IsRobotNetworkConnected(BuildingComponent toMoveBuildingComponent, List<Vector2I> robotCoverageAtDestination)
	{
		var baseBuilding = BuildingComponent.GetValidBuildingComponents(this)
			.First((buildingComponent) => buildingComponent.BuildingResource.IsBase);

		var visitedBuildings = new HashSet<BuildingComponent>();
		VisitAllConnectedRobots(baseBuilding, toMoveBuildingComponent, robotCoverageAtDestination, visitedBuildings);

		if(visitedBuildings.Contains(baseBuilding))
		{
			return true;
		}
		else
		{
			return false;
		}
	}

	private void VisitAllConnectedRobots(BuildingComponent rootBuilding, BuildingComponent toMoveRobot ,List<Vector2I> robotCoverageAtDestination, HashSet<BuildingComponent> visitedBuildings)
	{
		var connectedRobots = BuildingComponent.GetValidBuildingComponents(this)
			.Where((buildingComponent) =>
			{
				if (buildingComponent.BuildingResource.BuildableRadius == 0) return false;
				if (visitedBuildings.Contains(buildingComponent)) return false;

				var anyTilesInRadius = GetValidTilesInRadius(buildingComponent.GetTileArea(), buildingComponent.BuildingResource.BuildableRadius)
					.Any((tilePosition) => robotCoverageAtDestination.Contains(tilePosition));
				return buildingComponent != toMoveRobot && anyTilesInRadius;
			}).ToList();


		visitedBuildings.UnionWith(connectedRobots);
		if (visitedBuildings.Contains(rootBuilding)) return;

		foreach (var connectedRobot in connectedRobots)
		{
			VisitAllConnectedRobots(rootBuilding, connectedRobot, GetValidTilesInRadius(connectedRobot.GetTileArea(), connectedRobot.BuildingResource.BuildableRadius), visitedBuildings);
		}
	}

	private void VisitAllConnectedBuildings(BuildingComponent rootBuilding,	BuildingComponent excludeBuilding, HashSet<BuildingComponent> visitedBuildings
	)
	{
		var dependentBuildings = BuildingComponent.GetValidBuildingComponents(this)
			.Where((buildingComponent) =>
			{
				if (buildingComponent.BuildingResource.BuildableRadius == 0) return false;
				if (visitedBuildings.Contains(buildingComponent)) return false;

				var anyTilesInRadius = buildingComponent.GetOccupiedCellPositions()
					.All((tilePosition) => buildingToBuildableTiles[rootBuilding].Contains(tilePosition));
				return buildingComponent != excludeBuilding && anyTilesInRadius;
			}).ToList();

		visitedBuildings.UnionWith(dependentBuildings);
		foreach (var dependentBuilding in dependentBuildings)
		{
			VisitAllConnectedBuildings(dependentBuilding, excludeBuilding, visitedBuildings);
		}
	}

	private HashSet<Vector2I> GetBuildableTileSet(bool isAttackTiles = false)
	{
		return isAttackTiles ? validBuildableAttackTiles : validBuildableTiles;
	}

	private HashSet<Vector2I> GetValidTileSet()
	{
		return allTilesInBuildingRadius;
	}

	private List<TileMapLayer> GetAllTilemapLayers(Node2D rootNode)
	{
		var result = new List<TileMapLayer>();
		var children = rootNode.GetChildren();
		children.Reverse();
		foreach (var child in children)
		{
			if (child is Node2D childNode)
			{
				result.AddRange(GetAllTilemapLayers(childNode));
			}
		}

		if (rootNode is TileMapLayer tileMapLayer)
		{
			result.Add(tileMapLayer);
		}
		return result;
	}

	private List<Vector2I> GetAllBuildableBaseTerrainTiles(TileMapLayer tileMapLayer)
	{
		// Loop through all possible cells in the TileMap
        var usedTiles = tileMapLayer.GetUsedCells();
    	// Filter tiles where the custom data indicates they are buildable
    	var buildableTiles = usedTiles
        .Where(tilePosition =>
        {
            var (tileMapLayer, isBuildable) = GetTileCustomData(tilePosition, IS_BUILDABLE);
            return isBuildable;
        })
        .ToList();
		return buildableTiles;
	}

	private void MapTileMapLayersToElevationLayers()
	{
		foreach (var layer in allTilemapLayers)
		{
			ElevationLayer elevationLayer;
			Node startNode = layer;
			do
			{
				var parent = startNode.GetParent();
				elevationLayer = parent as ElevationLayer;
				startNode = parent;
			} while (elevationLayer == null && startNode != null);

			tileMapLayerToElevationLayer[layer] = elevationLayer;
		}
	}

	private void UpdateValidBuildableTiles(BuildingComponent buildingComponent, bool emitGridStateUpdated = true)
	{
		using (Telemetry.Scope("GridManager.UpdateValidBuildableTiles"))
		{
			occupiedTiles.UnionWith(buildingComponent.GetOccupiedCellPositions());
		var tileArea = buildingComponent.GetTileArea();

		if (buildingComponent.BuildingResource.BuildableRadius > 0)
		{
			var allTiles = GetTilesInRadiusFiltered(tileArea, buildingComponent.BuildingResource.BuildableRadius, (_) => true);
			allTilesInBuildingRadius.UnionWith(allTiles);

			var validTiles = GetValidTilesInRadius(tileArea, buildingComponent.BuildingResource.BuildableRadius);
			var validTilesPlusOne = GetValidTilesInRadius(tileArea, buildingComponent.BuildingResource.BuildableRadius + 1);
			buildingToBuildableTiles[buildingComponent] = validTiles.ToHashSet();
			validBuildableTiles.UnionWith(validTiles);
		}

		validBuildableTiles.ExceptWith(occupiedTiles);
		validBuildableAttackTiles.UnionWith(validBuildableTiles);

			validBuildableTiles.ExceptWith(dangerOccupiedTiles);
			if (emitGridStateUpdated)
			{
				EmitSignal(SignalName.GridStateUpdated);
			}
		}
	}

	private void SetBaseAntennaCoverage()
	{
		var buildingComponents = BuildingComponent.GetBaseBuilding(this);
		foreach(var buildingComponent in buildingComponents)
		{
			var baseOccupiedTiles = buildingComponent.GetOccupiedCellPositions();
			var tileArea = buildingComponent.GetTileArea();
			var allTiles = GetTilesInRadiusFiltered(tileArea, buildingComponent.BuildingResource.BuildableRadius, (_) => true);
			var allTilesRestrained = GetTilesInRadiusFiltered(tileArea, 1, (_) => true);
			baseProximityTiles = allTilesRestrained.ToHashSet();
			baseAntennaCoveredTiles = allTiles.ToHashSet();
		}
	}

	private void UpdateCollectedWoodTiles(BuildingComponent buildingComponent, bool emitGridStateUpdated = true)
	{
		if (buildingComponent.IsLifted) return;
		var tileArea = buildingComponent.GetTileArea();
		var resourceTiles = GetWoodTilesInRadius(tileArea, buildingComponent.BuildingResource.ResourceRadius);

		// Only collect new resource tiles if robot has capacity
		foreach (var tile in resourceTiles)
		{
			if (!collectedResourceTiles.Contains(tile) &&
				buildingComponent.resourceCollected.Count < buildingComponent.BuildingResource.ResourceCapacity)
			{
				collectedResourceTiles.Add(tile);
				buildingComponent.CollectResource(WOOD);
				EmitSignal(SignalName.ResourceTilesUpdated, tile, collectedResourceTiles.Count, WOOD);
			}
		}

		if (emitGridStateUpdated)
		{
			EmitSignal(SignalName.GridStateUpdated);
		}
	}

	private void UpdateCollectedMineralTiles(BuildingComponent buildingComponent, bool emitGridStateUpdated = true)
	{
		var tileArea = buildingComponent.GetTileArea();
		var mineralTilesWithType = GetMineralTilesInRadiusWithType(tileArea, buildingComponent.BuildingResource.ResourceRadius);

		// Only collect new mineral tiles if robot has capacity
		foreach (var (tile, mineralType) in mineralTilesWithType)
		{
			if (!collectedMineralTiles.Contains(tile) &&
				buildingComponent.resourceCollected.Count < buildingComponent.BuildingResource.ResourceCapacity)
			{
				collectedMineralTiles.Add(tile);
				buildingComponent.CollectResource(mineralType.ToString());

				// Emit the signal with tile count and mineral type as string
				EmitSignal(SignalName.MineralTilesUpdated, tile, collectedMineralTiles.Count, mineralType.ToString());
			}
		}
		if (emitGridStateUpdated)
		{
			EmitSignal(SignalName.GridStateUpdated);
		}
	}

	private void UpdateRechargeBattery(BuildingComponent buildingComponent)
	{
		var occupiedCellPositions = buildingComponent.GetOccupiedCellPositions();
		foreach (var position in occupiedCellPositions)
		{
			if (baseProximityTiles.Contains(position))
			{
				buildingComponent.SetRecharging(true);
			}
			else buildingComponent.SetRecharging(false);
		}
	}

	private void UpdateDiscoveredTiles(BuildingComponent buildingComponent, bool emitGridStateUpdated = true)
	{
		var tileArea = buildingComponent.GetTileArea();
		var discoveredTiles = GetDiscoveredTilesInRadius(tileArea, buildingComponent.BuildingResource.VisionRadius);

		var oldDiscoveredTileCount = discoveredElementsTiles.Count;
		discoveredElementsTiles.UnionWith(discoveredTiles.Keys);

		if (oldDiscoveredTileCount != discoveredElementsTiles.Count)
		{
			foreach(var entry in discoveredTiles)
			{
			EmitSignal(SignalName.DiscoveredTileUpdated, entry.Key, entry.Value);
			}
		}
		if (emitGridStateUpdated)
		{
			EmitSignal(SignalName.GridStateUpdated);
		}
	}

	private void RecalculateGrid()
	{
		//var stopwatch = new System.Diagnostics.Stopwatch();
		//stopwatch.Start();

		occupiedTiles.Clear();
		validBuildableTiles.Clear();
		validBuildableAttackTiles.Clear();
		allTilesInBuildingRadius.Clear();
		//collectedResourceTiles.Clear();
		//dangerOccupiedTiles.Clear();
		buildingToBuildableTiles.Clear();
		movementCoverageCacheInitialized = false;
		TileToBuilding.Clear();

		var buildingComponents = BuildingComponent.GetValidBuildingComponents(this);

		foreach (var buildingComponent in buildingComponents)
		{
			UpdateBuildingComponentGridState(buildingComponent, emitGridStateUpdated: false, updateBuildableCache: false);
			UpdateDiscoveredTiles(buildingComponent, emitGridStateUpdated: false);
			UpdateTilesToBuilding(buildingComponent);
			CheckGroundRobotTouchingMonolith(buildingComponent);
			CheckRobotHasVisualMonolith(buildingComponent);
		}
		var aerials = buildingComponents.Where(r => r.BuildingResource.IsAerial).ToList();
		if (aerials.Count > 0)
		{
			foreach (var aerial in aerials)
			{
				CheckStuckRobotNearby(aerial);
				CheckGroundRobotBelow(aerial);
			}
		}
		EmitSignal(SignalName.ResourceTilesUpdated, collectedResourceTiles.Count);
		EmitSignal(SignalName.GridStateUpdated);
		//stopwatch.Stop();
		//GD.Print($"RecalculateGrid took {stopwatch.ElapsedMilliseconds} ms");
	}

	private void UpdateTilesToBuilding(BuildingComponent buildingComponent)
	{
		var occupiedTiles = buildingComponent.GetOccupiedCellPositions();
		foreach( var tile in occupiedTiles)
		TileToBuilding[tile] = buildingComponent;	
	}

	private void CheckStuckRobotNearby(BuildingComponent buildingComponent)
	{
		var occupiedTilesExceptStuck = new HashSet<Vector2I>(buildingComponent.GetOccupiedCellPositions());
		bool isNear = false;
		foreach(var robot in buildingStuckToTiles.Keys)
		{
			HashSet<Vector2I> positions = buildingStuckToTiles[robot];
			occupiedTilesExceptStuck.ExceptWith(robot.GetOccupiedCellPositions());
        	isNear = positions.Any(position => occupiedTilesExceptStuck.Contains(position));
			if(isNear)
			{
				robot.SetToUnstuck();
			}
		}
	}

	private void CheckGroundRobotBelow(BuildingComponent buildingComponent)
	{
		bool bingo = false;
		var uavOccupiedTiles = buildingComponent.GetOccupiedCellPositions();

		foreach (var tile in occupiedTiles)
		{
			var aboveTile = tile + Vector2I.Up;
			if (uavOccupiedTiles.Contains(aboveTile))
			{
				var groundRobot = TileToBuilding[tile];
				if (groundRobot.BuildingResource.IsBase)
				{
					GameEvents.EmitNoGroundRobotBelowUav();
					return;
				}
				GameEvents.EmitGroundRobotBelowUav(groundRobot);
				bingo = true;
				return;
			}
		}
		if (!bingo)
		{
			GameEvents.EmitNoGroundRobotBelowUav();
		}
	}

	/// <summary>
	/// Gets the robot/building component at a specific grid position
	/// </summary>
	public BuildingComponent GetRobotAtPosition(Vector2I gridPosition)
	{
		if (TileToBuilding.TryGetValue(gridPosition, out var building))
		{
			// Don't return bases or antennas for lifting
			if (!building.BuildingResource.IsBase && building.BuildingResource.DisplayName != "Antenna")
			{
				return building;
			}
		}
		return null;
	}

	public Vector2I GetSampleLocationAroundPosition(Vector2I gridPosition)
	{
		IReadOnlyList<Vector2I> sampleLocations = GetSampleLocationsAroundPosition(gridPosition);
		return sampleLocations.Count > 0 ? sampleLocations[0] : new Vector2I(-1, -1);
	}

	public IReadOnlyList<Vector2I> GetSampleLocationsAroundPosition(Vector2I gridPosition)
	{
		List<Vector2I> sampleLocations = new();
		Vector2I[] samplePos = new Vector2I[]
		{
			gridPosition + Vector2I.Up,
			gridPosition + Vector2I.Down,
			gridPosition + Vector2I.Left,
			gridPosition + Vector2I.Right,
			gridPosition
		};
		foreach (var adjacentTile in samplePos)
		{
			if (monolithFragmentTiles.Contains(adjacentTile))
			{
				sampleLocations.Add(adjacentTile);
			}
		}

		return sampleLocations;
	}

	private void CheckGroundRobotTouchingMonolith(BuildingComponent buildingComponent)
	{
		if (buildingComponent.IsLifted) return;
		if (buildingComponent.BuildingResource.IsAerial)
		{
			foreach (var adjacentTile in buildingComponent.GetTileAndAdjacent())
			{
				if (monolithTiles.Contains(adjacentTile))
				{
					FloatingTextManager.ShowMessageAtBuildingPosition("Ground robot required to sample the monolith.", buildingComponent);
					return;
				}
			}
		}
		else if(buildingComponent.BuildingResource.IsBase)
        {
			foreach (var occupiedTile in buildingComponent.GetOccupiedCellPositions())
			{
				if (monolithTiles.Contains(occupiedTile))
				{
					EmitSignal(SignalName.BaseTouchingMonolith);
					return;
				}
			}
			return;
        }
		else
		{
			foreach (var adjacentTile in buildingComponent.GetTileAndAdjacent())
			{
				if (monolithTiles.Contains(adjacentTile))
				{
					EmitSignal(SignalName.GroundRobotTouchingMonolith);
					return;
				}
			}
		}
	}

	private void CheckRobotHasVisualMonolith(BuildingComponent buildingComponent)
	{
		foreach(var visionTile in GetTilesInRadiusInternal(buildingComponent.GetAreaOccupied(ConvertWorldPositionToTilePosition(buildingComponent.GlobalPosition)),buildingComponent.BuildingResource.VisionRadius))
		{
			if (monolithTiles.Contains(visionTile))
			{
				EmitSignal(SignalName.AerialRobotHasVisionOfMonolith);
				return;
			}
		}
	}

	private bool IsTileInsideCircle(Vector2 centerPosition, Vector2 tilePosition, float radius)
	{
		var distanceX = centerPosition.X - (tilePosition.X + .5);
		var distanceY = centerPosition.Y - (tilePosition.Y + .5);
		var distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
		return distanceSquared <= radius * radius;
	}

	private List<Vector2I> GetTilesInRadiusFiltered(Rect2I tileArea, int radius, Func<Vector2I, bool> filterFn)
	{
		var result = new List<Vector2I>();
		var tileAreaF = tileArea.ToRect2F();
		var tileAreaCenter = tileAreaF.GetCenter();
		var radiusMod = Mathf.Max(tileAreaF.Size.X, tileAreaF.Size.Y) / 2;

		for (var x = tileArea.Position.X - radius; x < tileArea.End.X + radius; x++)
		{
			for (var y = tileArea.Position.Y - radius; y < tileArea.End.Y + radius; y++)
			{
				var tilePosition = new Vector2I(x, y);
				if (!IsTileInsideCircle(tileAreaCenter, tilePosition, radius + radiusMod) || !filterFn(tilePosition)) continue;
				result.Add(tilePosition);
			}
		}
		return result;
	}

	/// <summary>
	/// Public wrapper to get all tiles within a radius of a building area
	/// Used for fog of war clearing and vision calculations
	/// </summary>
	public List<Vector2I> GetTilesInRadius(Rect2I tileArea, int radius)
	{
		return GetTilesInRadiusInternal(tileArea, radius);
	}

	private List<Vector2I> GetTilesInRadiusInternal(Rect2I tileArea, int radius)
	{
		var result = new List<Vector2I>();
		var tileAreaF = tileArea.ToRect2F();
		var tileAreaCenter = tileAreaF.GetCenter();
		var radiusMod = Mathf.Max(tileAreaF.Size.X, tileAreaF.Size.Y) / 2;

		for (var x = tileArea.Position.X - radius; x < tileArea.End.X + radius; x++)
		{
			for (var y = tileArea.Position.Y - radius; y < tileArea.End.Y + radius; y++)
			{
				var tilePosition = new Vector2I(x, y);
				if (!IsTileInsideCircle(tileAreaCenter, tilePosition, radius + radiusMod)) continue;
				result.Add(tilePosition);
			}
		}
		return result;
	}

	private List<Vector2I> GetValidTilesInRadius(Rect2I tileArea, int radius)
	{
		return GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			return GetTileCustomData(tilePosition, IS_BUILDABLE).Item2 || monolithTiles.Contains(tilePosition);
		});
	}

	private List<Vector2I> GetWoodTilesInRadius(Rect2I tileArea, int radius)
	{
		return GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			var isWood = GetTileCustomData(tilePosition, IS_WOOD).Item2;
			return isWood;
		});
	}

	private List<Vector2I> GetMineralTilesInRadius(Rect2I tileArea, int radius)
	{
		return GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			var isMineral = GetTileCustomData(tilePosition, IS_MINERAL).Item2;
			return isMineral;
		});
	}

	private List<(Vector2I tile, string mineralType)> GetMineralTilesInRadiusWithType(Rect2I tileArea, int radius)
	{
		var result = new List<(Vector2I, string)>();
		var tiles = GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			return GetTileCustomData(tilePosition, IS_MINERAL).Item2;
		});

		foreach (var tile in tiles)
		{
			var mineralTypeString = GetTileDiscoveredElements(tile);
			result.Add((tile, mineralTypeString));
		}
		return result;
	}

	private Dictionary<Vector2I, string> GetDiscoveredTilesInRadius(Rect2I tileArea, int radius)
	{
		Dictionary<Vector2I, string> tileToLandscapeType = new();
		var tilesInRadius  = GetTilesInRadiusInternal(tileArea, radius);
		string type;
		foreach (var tile in tilesInRadius)
		{
			type = GetTileDiscoveredElements(tile);
			if(type != "")
			{
				tileToLandscapeType.Add(tile, type);
			}
		}
		return tileToLandscapeType;
	}

	public void UpdateBuildingComponentGridState(BuildingComponent buildingComponent, bool emitGridStateUpdated = true, bool updateBuildableCache = true)
	{
		var buildingOccupiedTiles = buildingComponent.GetOccupiedCellPositions();
		if (updateBuildableCache)
		{
			UpdateValidBuildableTiles(buildingComponent, emitGridStateUpdated: false);
			buildableTileCacheDirty = false;
		}
		UpdateRechargeBattery(buildingComponent);
		UpdateCollectedWoodTiles(buildingComponent, emitGridStateUpdated: false);
		UpdateCollectedMineralTiles(buildingComponent, emitGridStateUpdated: false);
		if (emitGridStateUpdated)
		{
			EmitSignal(SignalName.GridStateUpdated);
		}
	}

	public bool IsTileMud(Vector2I tilePosition)
	{
		(_, bool isMud) = GetTileCustomData(tilePosition, IS_MUD);
		return isMud;
	}

	private void OnBuildingPlaced(BuildingComponent buildingComponent)
	{
		UpdateBuildingComponentGridState(buildingComponent, emitGridStateUpdated: false, updateBuildableCache: false);
		UpdateDiscoveredTiles(buildingComponent, emitGridStateUpdated: false);
		if(baseAntennaCoveredTiles.Count() == 0)
		{
			SetBaseAntennaCoverage();
		}
		buildableTileCacheDirty = true;
		movementCoverageCacheInitialized = false;
		EmitSignal(SignalName.GridStateUpdated);
	}

	private void OnBuildingMoved(BuildingComponent buildingComponent)
	{
		//ClearHighlightedTiles();
		buildableTileCacheDirty = true;
		if (movementCoverageCacheInitialized)
		{
			UpdateMovementCoverageForBuilding(buildingComponent);
			RebuildConnectedNetworkCache();
		}
		CallDeferred("RecalculateGrid");
		//HighlightBuildableTiles();
	}

	private void OnBuildingStuck(BuildingComponent buildingComponent)
	{
		var tileAreaOccupied = buildingComponent.GetTileAndAdjacent();
		buildingStuckToTiles[buildingComponent] = tileAreaOccupied;
	}

	private void OnBuildingUnStuck(BuildingComponent buildingComponent)
	{
		buildingStuckToTiles.Remove(buildingComponent);
	}

	private void OnBuildingDestroyed(BuildingComponent buildingComponent)
	{
		buildableTileCacheDirty = true;
		movementCoverageCacheInitialized = false;
		RecalculateGrid();
	}

	private void OnBuildingEnabled(BuildingComponent buildingComponent)
	{
		UpdateBuildingComponentGridState(buildingComponent, updateBuildableCache: false);
		buildableTileCacheDirty = true;
		movementCoverageCacheInitialized = false;
	}

	private void OnBuildingDisabled(BuildingComponent buildingComponent)
	{
		buildableTileCacheDirty = true;
		movementCoverageCacheInitialized = false;
		RecalculateGrid();
	}

	private void EnsureMovementCoverageCache()
	{
		if (movementCoverageCacheInitialized)
		{
			return;
		}

		buildingToBuildableTiles.Clear();
		connectedNetworkCoverageTiles.Clear();
		connectedNetworkBuildings.Clear();

		foreach (var buildingComponent in BuildingComponent.GetValidBuildingComponents(this))
		{
			UpdateMovementCoverageForBuilding(buildingComponent);
		}

		RebuildConnectedNetworkCache();
		movementCoverageCacheInitialized = true;
	}

	private void UpdateMovementCoverageForBuilding(BuildingComponent buildingComponent)
	{
		if (buildingComponent.BuildingResource.BuildableRadius <= 0)
		{
			buildingToBuildableTiles.Remove(buildingComponent);
			return;
		}

		buildingToBuildableTiles[buildingComponent] = GetValidTilesInRadius(
			buildingComponent.GetTileArea(),
			buildingComponent.BuildingResource.BuildableRadius).ToHashSet();
	}

	private void EnsureBuildableTileCache()
	{
		if (!buildableTileCacheDirty)
		{
			return;
		}

		using (Telemetry.Scope("GridManager.EnsureBuildableTileCache"))
		{
			EnsureMovementCoverageCache();
			validBuildableTiles.Clear();
			validBuildableAttackTiles.Clear();
			allTilesInBuildingRadius.Clear();

			foreach (var entry in buildingToBuildableTiles)
			{
				allTilesInBuildingRadius.UnionWith(entry.Value);
				validBuildableTiles.UnionWith(entry.Value);
			}
			validBuildableTiles.ExceptWith(occupiedTiles);
			validBuildableAttackTiles.UnionWith(validBuildableTiles);
			validBuildableTiles.ExceptWith(dangerOccupiedTiles);

			buildableTileCacheDirty = false;
		}
	}

	private void RebuildConnectedNetworkCache()
	{
		var networkBuildings = buildingToBuildableTiles.Keys
			.Where(buildingComponent => buildingComponent.BuildingResource.BuildableRadius > 0)
			.ToList();

		var baseBuilding = networkBuildings.FirstOrDefault(buildingComponent => buildingComponent.BuildingResource.IsBase);
		if (baseBuilding == null)
		{
			return;
		}

		var pendingBuildings = new Queue<BuildingComponent>();
		pendingBuildings.Enqueue(baseBuilding);
		connectedNetworkBuildings.Add(baseBuilding);

		if (buildingToBuildableTiles.TryGetValue(baseBuilding, out var baseCoverage))
		{
			connectedNetworkCoverageTiles.UnionWith(baseCoverage);
		}

		while (pendingBuildings.Count > 0)
		{
			var currentBuilding = pendingBuildings.Dequeue();
			if (!buildingToBuildableTiles.TryGetValue(currentBuilding, out var currentCoverage))
			{
				continue;
			}

			connectedNetworkCoverageTiles.UnionWith(currentCoverage);

			foreach (var candidate in networkBuildings)
			{
				if (connectedNetworkBuildings.Contains(candidate))
				{
					continue;
				}

				if (!buildingToBuildableTiles.TryGetValue(candidate, out var candidateCoverage))
				{
					continue;
				}

				if (!candidateCoverage.Any(tilePosition => connectedNetworkCoverageTiles.Contains(tilePosition)))
				{
					continue;
				}

				connectedNetworkBuildings.Add(candidate);
				pendingBuildings.Enqueue(candidate);
			}
		}
	}
	private void ClearAll()
	{
    allTilesBuildableOnTheMap.Clear();
    validBuildableTiles.Clear();
    validBuildableAttackTiles.Clear();
    allTilesInBuildingRadius.Clear();
    collectedResourceTiles.Clear();
    collectedMineralTiles.Clear();
    discoveredElementsTiles.Clear();
    occupiedTiles.Clear();
    dangerOccupiedTiles.Clear();
    baseAntennaCoveredTiles.Clear();
    baseProximityTiles.Clear();
    monolithTiles.Clear();
    TileToBuilding.Clear();
    buildingToBuildableTiles.Clear();
    buildingStuckToTiles.Clear();
	    connectedNetworkCoverageTiles.Clear();
	    connectedNetworkBuildings.Clear();
	    movementCoverageCacheInitialized = false;
	    buildableTileCacheDirty = true;
    // Reset other state as needed
	}
}
