

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using CrownRelayServer;
using Godot;

public partial class Player : Node, IRelayedObject
{
	// References
	public MainUI UI { get; private set; }
	private HexGrid Grid;
	public PlayerController Controller { get; private set; }

	// Multiplayer Player Info
	[Signal]
	public delegate void PlayerNameChangedEventHandler();
	private void EmitPlayerNameChanged() { EmitSignal(SignalName.PlayerNameChanged); }
	public string PlayerName
	{
		get { return _playerName; }
		set
		{
			_playerName = value;
			CallDeferred(MethodName.EmitPlayerNameChanged);
		}
	}
	private string _playerName = "unnamed player";
	public int PlayerNumber = 0;
	public string RelayedID { get { return PlayerName; } }

	// Player Cosmetics
	public Mesh CaptureMesh;
	public StandardMaterial3D PlayerColorMaterial;

	// Points
	public Godot.Collections.Dictionary<string, int> Points = new();
	public int ArmyPoints { get => Points["army"]; set { Points["army"] = value; } }
	public int GoldPoints { get => Points["gold"]; set { Points["gold"] = value; } }
	public int BuildingPoints { get => Points["building"]; set { Points["building"] = value; } }
	public int ReputationPoints { get => Points["reputation"]; set { Points["reputation"] = value; GameManager.CheckWinState(); } }
	public bool HoldsCrown = false;

	// Trading Discounts
	public Godot.Collections.Dictionary<string, int> Discounts { get; private set; } = new()
	{
		["army"] = 0,
		["gold"] = 0,
		["building"] = 0,
		["reputation"] = 0
	};

	// War
	public int ActiveTroops = 0;

	// Tiles
	public List<Tile> Tiles = new();
	public Dictionary<int, List<Tile>> LeyLines = new();
	public HashSet<Player> RaidablePlayers = new();
	public List<Tile> AttackableTiles = new();

	// Buildings
	public List<Building> Buildings = new();

	// Card Hand
	public List<BuildingCard> Hand { get; private set; } = new();

	public Player Initialize(string playerName, int playerNumber)
	{
		// init props
		PlayerName = playerName;
		PlayerNumber = playerNumber;

		Points = new Godot.Collections.Dictionary<string, int>
		{
			["building"] = 0,
			["army"] = 0,
			["gold"] = 0,
			["reputation"] = 0
		};

		// init player cosmetics
		CaptureMesh = ResourceLoader.Load<Mesh>("res://Source/Castles/Meshes/castles_CapturePiece.res");
		PlayerColorMaterial = ResourceLoader.Load<StandardMaterial3D>($"res://Materials/Players/player_{playerNumber + 1}_mat.tres");

		return this;
	}

	public override void _Ready()
	{
		RelayServer.RegisterInstance(this);

		UI = GetNode<MainUI>("../MainUI");

		Grid = GetNode<HexGrid>("../HexGrid");
		Controller = GetNode<PlayerController>("../PlayerController");

		if (PlayerNumber == 0)
		{
			GameManager.Players.Add(this);
			Initialize(PlayerName, PlayerNumber);
		}

		UI.AddPlayerSlate(this);
	}


	public void AddPoints(Godot.Collections.Dictionary<string, int> newPoints)
	{
		foreach ((string name, int amount) in newPoints)
		{
			Points[name] += amount;
		}

		GameManager.CheckWinState();
	}

	public void LosePoints(Godot.Collections.Dictionary<string, int> lostPoints)
	{
		foreach ((string name, int amount) in lostPoints)
		{
			int newValue = Points[name] - amount;
			Points[name] = newValue >= 0 ? newValue : 0;
		}
	}

	/// <summary>
	/// Captures tile for player specified by playerId, and coordId
	/// </summary>
	/// 
	/// <param name="command">command.Kwargs requires { "id": string }</param>
	public void CaptureTile(PCommand command)
	{
		string coordId = command.Kwargs["id"];

		GD.Print($"{GameManager.LocalPlayer.PlayerName}~ {PlayerName} - Capture Tile: {coordId} - Expansion Type: {GameManager.PlayerExpansionType}");
		Tile tile = Grid.Grid[AxialCoord.FromIdString(coordId)];

		Tile[] validTiles;
		switch (GameManager.PlayerExpansionType)
		{
			case ExpansionType.Adjacent:
				validTiles = GetNeighborCoords().Select(c =>
				{
					Tile nTile;
					if (Grid.Grid.TryGetValue(c, out nTile)) return nTile; else return null;
				}).Where(i => i != null).ToArray();
				break;
			case ExpansionType.LeyLine:
				GD.Print(LeyLines.Values.Count());
				validTiles = LeyLines.Values.SelectMany(t => t).ToArray();
				break;
			default:
				validTiles = Grid.Grid.Values.ToArray();
				break;
		}

		// don't allow if tile is already captured or isn't adjacent to player.
		if (tile.OwnedBy != null | !validTiles.Contains(tile))
		{
			GD.Print($"\n{GameManager.LocalPlayer.PlayerName}~ ERROR: Can't Capture tile");
			GD.Print($"Valid Tiles (Count: {validTiles.Count()}): {validTiles.Select(t => t.Coord.ToString()).ToArray().Join(", ")}");
			return;
		}

		tile.Capture(this);
		AddPoints(tile.Resources);

		GameManager.UpdateAllLeyLines();

		UI.Update();
		GameManager.StartActionPhase();
	}

	/// <summary>
	/// Place building on tile using AxialId and BuildingCard Mint
	/// </summary>
	/// 
	/// <param name="command">
	/// Command.Kwargs requires { "id": string, "mint": int }
	/// </param>
	public void PlaceBuilding(PCommand command)
	{
		string coordId = command.Kwargs["id"];
		int buildingCardMint = command.Kwargs["mint"].ToInt();

		Tile tile = Grid.Grid[AxialCoord.FromIdString(coordId)];

		BuildingCard buildingCard = Hand.Find(card => card.Mint == buildingCardMint);
		if (GoldPoints < buildingCard.Price)
		{
			GD.Print($"Insufficent gold, Gold Points: {GoldPoints} Price: {buildingCard.Price}");
			return;
		}
		else if (tile.OwnedBuilding != null)
		{
			GD.Print($"Tile {coordId} Already Contains a building");
			return;
		}

		if (buildingCard.BuildsOnEnemy) // Steal tile if it's placed on enemy
		{
			tile.NewOwner(this);
			AddPoints(tile.Resources);

			GameManager.UpdateAllLeyLines();
		}

		tile.PlaceBuilding(buildingCard.BuildingScene.Instantiate<Building>());
		GoldPoints -= buildingCard.Price;

		// Update UI
		Hand.Remove(buildingCard);
		UI.DestroyActiveBuildingCard();
		UI.Update();
		GameManager.EndTurn();
	}

	/// <summary>
	/// Player Attacks tile based on coordId
	/// </summary>
	/// <param name="command">command.Kwargs requires { "id": string }</param>
	public void AttackTile(PCommand command)
	{
		_ = AttackTileAsync(command.Kwargs["id"]);
	}

	public async Task AttackTileAsync(string coordId)
	{
		Tile tile = Grid.Grid[AxialCoord.FromIdString(coordId)];
		Player defender = tile.OwnedBy;

		if (!AttackableTiles.Contains(tile))
		{
			GD.Print($"Tile {coordId} is not attackable by {PlayerName}");
		}

		// Get Tile buffs
		int attackerBuffs = this.GetTileBuffsCount(tile, BuffType.Offense);
		int defenderBuffs = defender.GetTileBuffsCount(tile, BuffType.Defense);

		await GameManager.StartWar(this, defender); // Opens war UI for all players

		// Both players roll
		int attackerRoll = await GameManager.Roll();
		int defenderRoll = await GameManager.Roll();

		// Compile scores
		int attackerScore = attackerRoll + attackerBuffs + this.ActiveTroops;
		int defenderScore = defenderRoll + defenderBuffs + defender.ActiveTroops;

		GD.Print($"Attacker Score ({attackerScore}): {attackerRoll} + {attackerBuffs} + {this.ActiveTroops}\nDefender Score ({defenderScore}): {defenderRoll} + {defenderBuffs} + {defender.ActiveTroops}");

		// Determine winner
		if (attackerScore > defenderScore)
		{
			GD.Print($"{this.PlayerName} Triumphs!");
			tile.DestroyBuilding();
			tile.NewOwner(this);

			defender.LosePoints(new() { ["reputation"] = 1, ["army"] = defender.ActiveTroops }); // Defender Loses one reputation point
			this.ArmyPoints -= ActiveTroops;

			this.AddPoints(tile.Resources); // Attacker gains tile resources

			GameManager.UpdateAllLeyLines();
		}
		else if (attackerScore <= defenderScore)
		{
			GD.Print($"{defender.PlayerName} Outlasts!");

			defender.ReputationPoints += 1;
			this.ArmyPoints -= ActiveTroops;
		}

		UI.Update();
		GameManager.EndTurn();
	}

	public AxialCoord[] GetNeighborCoords()
	{
		return Tiles.SelectMany(t => t.GetAdjacentCoords()).Distinct().ToArray();
	}


	public void BuildLeyLines()
	{
		LeyLines.Clear();
		RaidablePlayers.Clear();
		AttackableTiles.Clear();

		List<AxialCoord> CheckedCoords = new();
		List<Tile> CurrentStep = Tiles.ToList();

		int step = 1;

		while (CurrentStep.Count != 0)
		{
			List<Tile> NextStep = new();
			LeyLines[step] = new List<Tile>();

			foreach (Tile tile in CurrentStep)
			{
				// Don't allow leylines to path off of uncaptured tiles not openToLeyLines
				if (!tile.OpenToLeyLine & tile.OwnedBy != this) continue;

				foreach (Tile neighbor in tile.Neighbors)
				{
					if (neighbor.OwnedBy != null)
					{
						if (step == 1) AttackableTiles.Add(neighbor); // If one of the connecting tiles is directly adjacent to one of players tiles, make them attackable.
						if (neighbor.OpenToLeyLine) RaidablePlayers.Add(neighbor.OwnedBy); // If one of the connecting routes was a player and on a ground piece, add them to the raidable options
						continue;
					}

					if (!neighbor.Capturable |
							(!neighbor.OpenToLeyLine & step != 1) | // Adjacent tiles are allowed on leylines as well, even when not openToLeyLines
							CheckedCoords.Contains(neighbor.Coord)
					)
					{
						continue;
					}

					CheckedCoords.Add(neighbor.Coord);
					NextStep.Add(neighbor);
					LeyLines[step].Add(neighbor);
				}
			}

			CurrentStep = NextStep.ToList();

			step++;
		}

		LeyLines.Remove(step - 1);
	}

	/// <summary>
	/// Draws a new card, only HOST should call this function, and should use no parameters
	/// </summary>
	public void DrawCard()
	{
		BuildingCard card = GameManager.DrawCard();

		if (RelayServer.IsActive)
		{
			RelayServer.RpcClients(new()
			{
				ObjectTypeString = typeof(Player).FullName,
				MethodStringName = MethodName._ReplicateCard,
				ObjectID = RelayedID,
				Kwargs = {
					["id"] = card.ID.ToString(),
					["mint"] = card.Mint.ToString()
				}
			});
		}

		ProcessNewCard(card);
	}


	/// <summary>
	/// Replicates card drawn from server to clients
	/// </summary>
	/// <param name="command">command.Kwargs { "id": int, "mint": int }</param>
	public void _ReplicateCard(PCommand command)
	{
		int cardId = command.Kwargs["id"].ToInt();
		int cardMint = command.Kwargs["mint"].ToInt();

		BuildingCard card = GameManager.BuildingCardLookup[cardId].Instantiate<BuildingCard>().Initialize(cardMint);
		ProcessNewCard(card);
	}

	private void ProcessNewCard(BuildingCard card)
	{
		Hand.Add(card);
		BuildingPoints -= GameManager.DefaultBuildingCost;

		if (this == GameManager.LocalPlayer) UI.AddCardToHand(card);
		UI.Update();
		GameManager.EndTurn();
	}

	/// <summary>
	/// Raids player based on player number.
	/// </summary>
	/// <param name="command">command.Kwargs requires { "target": int }</param>
	public void RaidPlayer(PCommand command)
	{
		Player victim = GameManager.Players[command.Kwargs["target"].ToInt()];

		string stolenPointType = victim.Points["gold"] > 0 ? "gold" : "reputation";
		Godot.Collections.Dictionary<string, int> stolenPoints = new() { [stolenPointType] = 1 };

		victim.LosePoints(stolenPoints);
		AddPoints(stolenPoints);

		UI.Update();
		GameManager.EndTurn();
	}

	/// <summary>
	/// Trades gold for specific resource and price.
	/// </summary>
	/// <param name="command">command.Kwargs requires { "resource": string, "price": int }</param>
	public void TradeResource(PCommand command)
	{
		string resourceType = command.Kwargs["resource"];
		int price = command.Kwargs["price"].ToInt();

		if (GoldPoints < price) return;

		AddPoints(new() { [resourceType] = 1 });
		LosePoints(new() { ["gold"] = price });

		UI.Update();
	}

	public void Upkeep()
	{
		if (HoldsCrown) GameManager.CrownWillAvailable = true;

		foreach (Building building in Buildings)
		{
			IBuildingUpkeep upkeepBuilding = building as IBuildingUpkeep;

			if (upkeepBuilding != null) upkeepBuilding.Upkeep();
		}

		UI.Update();
	}

	public void IncrementDiscount(string discountType, int weight)
	{
		Discounts[discountType] += weight;
	}

	public int GetTileBuffsCount(Tile tile, BuffType type)
	{
		int buffCounter = 0;

		foreach (Buff buff in GetTileBuffs(tile).FindAll(b => b.Type == type))
		{
			buffCounter += buff.Scalar;
		}

		return buffCounter;
	}

	public List<Buff> GetTileBuffs(Tile tile)
	{
		List<Buff> buffs = new();

		GD.Print($"{this.PlayerName} Buildings Count: {Buildings.Count()}");

		foreach (Building building in Buildings)
		{
			IBuildingWar warBuilding = building as IBuildingWar;

			if (warBuilding is null || !building.OwnerTile.Neighbors.Contains(tile)) continue;

			buffs.Add(warBuilding.GetBuff());
		}

		return buffs;
	}
}
