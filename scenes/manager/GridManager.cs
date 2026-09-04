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
	public sealed class AntennaCoverageVisualLayer
	{
		public BuildingComponent Building { get; }
		public HashSet<Vector2I> Coverage { get; }
		public HashSet<Vector2I> UpstreamCoverage { get; }
		public bool IsBase => Building.BuildingResource.IsBase;

		internal AntennaCoverageVisualLayer(
			BuildingComponent building,
			HashSet<Vector2I> coverage,
			HashSet<Vector2I> upstreamCoverage)
		{
			Building = building;
			Coverage = coverage;
			UpstreamCoverage = upstreamCoverage;
		}
	}

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
	private Dictionary<BuildingComponent, HashSet<Vector2I>> buildingToAntennaCoveredTiles = new();
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
		baseProximityTiles = GetTilesInRadiusFiltered(baseArea, 1, (_) => true).ToHashSet();
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
	/// Finds the exact TileMapLayer that owns a resource at this position. Resource
	/// layers may exist both below BaseTerrainTileMapLayer and inside one or more
	/// ElevationLayers, so the generic first-cell lookup cannot be used here.
	/// </summary>
	private TileMapLayer GetResourceLayer(Vector2I tilePosition, string resourceDataName)
	{
		foreach (var layer in allTilemapLayers)
		{
			var customData = layer.GetCellTileData(tilePosition);
			if (customData == null || (bool)customData.GetCustomData(IS_IGNORED))
			{
				continue;
			}

			if (!(bool)customData.GetCustomData(resourceDataName))
			{
				continue;
			}

			// A harvested tree remains present in its TileMapLayer as a trunk, but it
			// is no longer an available wood resource or an obstacle for a drone.
			if (resourceDataName == IS_WOOD && collectedResourceTiles.Contains(tilePosition))
			{
				continue;
			}

			return layer;
		}

		return null;
	}

	/// <summary>
	/// Returns terrain data used by ground movement, ignoring resource and visual
	/// overlays drawn over the actual terrain. Cliff shadows can extend onto a
	/// lower surface, so their parent ElevationLayer must not define that tile's
	/// elevation. Harvested trees remain in the TileMap as trunks, which is why
	/// the raw is_wood flag is used rather than the runtime collection state.
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

			if (IsVisualOnlyLayer(layer) ||
				(bool)customData.GetCustomData(IS_WOOD) ||
				(bool)customData.GetCustomData(IS_MINERAL))
			{
				continue;
			}

			return (layer, (bool)customData.GetCustomData(dataName));
		}

		return (null, false);
	}

	private static bool IsVisualOnlyLayer(TileMapLayer layer)
	{
		string layerName = layer.Name.ToString();
		return layerName is "ShadowLayer" or "SecondaryShadowLayer" or
			"FoamTileMapLayer" or "CloudLayer";
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
			var landscapeType = (string)customData.GetCustomData("landscape_type");
			if (!string.IsNullOrEmpty(landscapeType))
			{
				return landscapeType;
			}
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

	/// <summary>
	/// Returns whether a robot can be deployed from the base at this area.
	/// Ground robots must be on the same elevation layer as the base so a base
	/// beside a cliff cannot deploy them directly uphill or downhill.
	/// </summary>
	public bool IsRobotDeploymentAreaValid(Rect2I tileArea, bool isAerial)
	{
		if (!baseProximityTiles.Contains(tileArea.Position) || !IsTileAreaBuildable(tileArea))
		{
			return false;
		}

		if (isAerial)
		{
			return true;
		}

		var baseTiles = baseArea.ToTiles();
		var deploymentTiles = tileArea.ToTiles();
		if (baseTiles.Count == 0 || deploymentTiles.Count == 0)
		{
			return false;
		}

		var (baseElevationLayer, _) = GetElevationLayerForTile(baseTiles[0]);
		return deploymentTiles.All(tile =>
		{
			var (deploymentElevationLayer, _) = GetElevationLayerForTile(tile);
			return deploymentElevationLayer == baseElevationLayer;
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

				return GetResourceLayer(tilePosition, IS_WOOD) == null;
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

	/// <summary>
	/// Highlights only the valid root cells from which the selected robot can be
	/// deployed. This deliberately bypasses the general highlight preference:
	/// these cells are placement affordances shown only while in deploy mode.
	/// </summary>
	public void HighlightRobotDeploymentTiles(Vector2I dimensions, bool isAerial)
	{
		using (Telemetry.Scope("GridManager.HighlightRobotDeploymentTiles"))
		{
			EnsureBuildableTileCache();
			foreach (var tilePosition in baseProximityTiles)
			{
				var deploymentArea = new Rect2I(tilePosition, dimensions);
				if (IsRobotDeploymentAreaValid(deploymentArea, isAerial))
				{
					highlightTilemapLayer.SetCell(tilePosition, 0, Vector2I.Zero);
				}
			}
		}
	}

	public void HighlightAntennaDeploymentTiles(
		BuildingComponent deployingRobot,
		Vector2I antennaDimensions)
	{
		if (!IsInstanceValid(deployingRobot))
		{
			return;
		}

		EnsureMovementCoverageCache();
		var connectedCoverage = BuildConnectedAntennaNetwork(deployingRobot)
			.SelectMany(layer => layer.Coverage)
			.ToHashSet();
		var robotTiles = deployingRobot.GetOccupiedCellPositions();

		foreach (var tilePosition in deployingRobot.GetTileAndAdjacent().Except(robotTiles))
		{
			var deploymentArea = new Rect2I(tilePosition, antennaDimensions);
			bool isValid = IsTileAreaBuildable(deploymentArea) &&
				deploymentArea.ToTiles().Any(connectedCoverage.Contains);
			var atlasCoords = isValid ? new Vector2I(1, 0) : new Vector2I(2, 0);
			highlightTilemapLayer.SetCell(tilePosition, 0, atlasCoords);
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
		
		if (toMoveBuildingComponent.BuildingResource.BuildableRadius <= 0)
		{
			return false;
		}

		return IsRobotNetworkConnected(toMoveBuildingComponent, destinationArea);
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

	private bool IsRobotNetworkConnected(BuildingComponent toMoveBuildingComponent, Rect2I destinationArea)
	{
		var destinationTiles = destinationArea.ToTiles();
		return BuildConnectedAntennaNetwork(toMoveBuildingComponent)
			.Any(layer => destinationTiles.Any(layer.Coverage.Contains));
	}

	public List<AntennaCoverageVisualLayer> GetConnectedAntennaCoverageLayers()
	{
		EnsureMovementCoverageCache();
		return BuildConnectedAntennaNetwork();
	}

	public bool IsAreaWithinConnectedAntennaCoverage(
		Rect2I tileArea,
		BuildingComponent excludedBuilding = null)
	{
		EnsureMovementCoverageCache();
		var areaTiles = tileArea.ToTiles();
		return BuildConnectedAntennaNetwork(excludedBuilding)
			.Any(layer => areaTiles.Any(layer.Coverage.Contains));
	}

	private List<AntennaCoverageVisualLayer> BuildConnectedAntennaNetwork(BuildingComponent excludedBuilding = null)
	{
		var result = new List<AntennaCoverageVisualLayer>();
		var networkBuildings = buildingToAntennaCoveredTiles.Keys
			.Where(buildingComponent =>
				buildingComponent != excludedBuilding &&
				buildingComponent.BuildingResource.BuildableRadius > 0)
			.OrderBy(buildingComponent => buildingComponent.GetInstanceId())
			.ToList();

		var baseBuilding = networkBuildings.FirstOrDefault(buildingComponent => buildingComponent.BuildingResource.IsBase);
		if (baseBuilding == null)
		{
			return result;
		}

		var visitedBuildings = new HashSet<BuildingComponent> { baseBuilding };
		var pendingBuildings = new Queue<BuildingComponent>();
		pendingBuildings.Enqueue(baseBuilding);
		result.Add(new AntennaCoverageVisualLayer(
			baseBuilding,
			buildingToAntennaCoveredTiles[baseBuilding],
			new HashSet<Vector2I>()));

		while (pendingBuildings.Count > 0)
		{
			var connectedBuilding = pendingBuildings.Dequeue();
			if (!buildingToAntennaCoveredTiles.TryGetValue(connectedBuilding, out var connectedCoverage))
			{
				continue;
			}

			foreach (var candidate in networkBuildings)
			{
				if (visitedBuildings.Contains(candidate) ||
					!candidate.GetTileArea().ToTiles().Any(connectedCoverage.Contains))
				{
					continue;
				}

				visitedBuildings.Add(candidate);
				pendingBuildings.Enqueue(candidate);
				result.Add(new AntennaCoverageVisualLayer(
					candidate,
					buildingToAntennaCoveredTiles[candidate],
					connectedCoverage));
			}
		}

		return result;
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
			if (IsResourceOnSameElevation(buildingComponent, tile, IS_WOOD) &&
				!collectedResourceTiles.Contains(tile) &&
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
			if (IsResourceOnSameElevation(buildingComponent, tile, IS_MINERAL) &&
				!collectedMineralTiles.Contains(tile) &&
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

	private bool IsResourceOnSameElevation(
		BuildingComponent buildingComponent,
		Vector2I resourceTile,
		string resourceDataName)
	{
		// Aerial units currently have no collection radius, but keep their existing behavior if
		// a future unit gains one. Ground collectors must share the exact terrain elevation node.
		if (buildingComponent.BuildingResource.IsAerial)
		{
			return true;
		}

		var (roverElevation, _) =
			GetElevationLayerForTile(buildingComponent.GetGridCellPosition());
		var resourceLayer = GetResourceLayer(resourceTile, resourceDataName);
		if (resourceLayer == null)
		{
			return false;
		}

		tileMapLayerToElevationLayer.TryGetValue(resourceLayer, out var resourceElevation);
		return roverElevation == resourceElevation;
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
			return GetResourceLayer(tilePosition, IS_WOOD) != null;
		});
	}

	private List<Vector2I> GetMineralTilesInRadius(Rect2I tileArea, int radius)
	{
		return GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			return GetResourceLayer(tilePosition, IS_MINERAL) != null;
		});
	}

	private List<(Vector2I tile, string mineralType)> GetMineralTilesInRadiusWithType(Rect2I tileArea, int radius)
	{
		var result = new List<(Vector2I, string)>();
		var tiles = GetTilesInRadiusFiltered(tileArea, radius, (tilePosition) =>
		{
			return GetResourceLayer(tilePosition, IS_MINERAL) != null;
		});

		foreach (var tile in tiles)
		{
			var mineralLayer = GetResourceLayer(tile, IS_MINERAL);
			var mineralData = mineralLayer?.GetCellTileData(tile);
			if (mineralData == null)
			{
				continue;
			}

			var mineralTypeString = (string)mineralData.GetCustomData("landscape_type");
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
		buildingToAntennaCoveredTiles.Clear();

		foreach (var buildingComponent in BuildingComponent.GetValidBuildingComponents(this))
		{
			UpdateMovementCoverageForBuilding(buildingComponent);
		}

		movementCoverageCacheInitialized = true;
	}

	private void UpdateMovementCoverageForBuilding(BuildingComponent buildingComponent)
	{
		if (buildingComponent.BuildingResource.BuildableRadius <= 0)
		{
			buildingToBuildableTiles.Remove(buildingComponent);
			buildingToAntennaCoveredTiles.Remove(buildingComponent);
			return;
		}

		buildingToBuildableTiles[buildingComponent] = GetValidTilesInRadius(
			buildingComponent.GetTileArea(),
			buildingComponent.BuildingResource.BuildableRadius).ToHashSet();
		buildingToAntennaCoveredTiles[buildingComponent] = GetTilesInRadiusFiltered(
			buildingComponent.GetTileArea(),
			buildingComponent.BuildingResource.BuildableRadius,
			(_) => true).ToHashSet();
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
	buildingToAntennaCoveredTiles.Clear();
    buildingStuckToTiles.Clear();
	    movementCoverageCacheInitialized = false;
	    buildableTileCacheDirty = true;
    // Reset other state as needed
	}
}
