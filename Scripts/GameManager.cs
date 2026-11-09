using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using CrownRelayServer;
using Godot;
using Godot.Collections;

public partial class GameManager : Node3D, IRelayedObject
{
	// Game References
	public static MainUI UI { get => GameManagerInstance._ui; }
	[Export] private MainUI _ui;

	// Player Information
	public static System.Collections.Generic.List<Player> Players { get; private set; } = new();
	public static Player LocalPlayer
	{
		get
		{
			if (RelayServer.IsActive)
			{
				return Players[RelayServer.LocalPlayerNumber];
			}
			return Players[0];
		}
	}

	public static Player CrownBearer;
	public static bool CrownWillAvailable = true;

	// Get references to game instance in scene
	public static GameManager GameManagerInstance;
	public string RelayedID { get; } = "0";

	// Game Instance Props
	public static int PointsToWin = 28;
	[Export] private int _pointToWin = 28;
	public static int MinPointsForCrown { get => GameManagerInstance._minPointsForCrown; }
	[Export] private int _minPointsForCrown = 5;

	// Signals
	[Signal]
	public delegate void PlayerTurnPhaseChangedEventHandler();

	[Signal]
	public delegate void GameStartedEventHandler();

	// Game State
	public static bool IsGameStarted = false;
	public static int PlayerTurn { get; private set; } = 0;
	public static Player ActivePlayer { get { return Players[PlayerTurn]; } }
	public static bool IsLocalPlayerTurn { get { return PlayerTurn == RelayServer.LocalPlayerNumber; } }

	public static TurnPhase PlayerTurnPhase
	{
		get
		{
			return _PlayerTurnPhase;
		}
		private set
		{
			_PlayerTurnPhase = value;

			GD.Print($"{ActivePlayer.PlayerName}, {_PlayerTurnPhase} Phase Started");
			UI.UpdateTurnTracker();
			GameManagerInstance.EmitSignal(SignalName.PlayerTurnPhaseChanged);
		}
	}

	private static TurnPhase _PlayerTurnPhase = TurnPhase.Upkeep;
	public static ExpansionType PlayerExpansionType { get; private set; } = ExpansionType.Open;
	public static Player WinningPlayer;

	// Synchronized Rolling
	private static TaskCompletionSource<bool> RollCompletionSource = null;
	private static int ReplicatedRoll = 0;

	// Synchronized War
	private static TaskCompletionSource<bool> WarCompletionSource;
	private static Player Attacker;
	private static Player Defender;

	// Building Cards
	[ExportGroup("Building Cards")]
	[Export] private int _maxHandSize = 3;
	public static int MaxHandSize { get => GameManagerInstance._maxHandSize; }
	public static int DefaultBuildingCost { get => GameManagerInstance._maxHandSize; }
	[Export] private int _defaultBuildingCost = 3;
	[Export] private Dictionary<PackedScene, int> DefaultDeck = new();
	private static System.Collections.Generic.List<BuildingCard> Deck = new();
	public static PackedScene[] BuildingCardLookup { get; private set; }

	public override void _Ready()
	{
		GameManagerInstance = this;
		RelayServer.RegisterInstance(this);

		InitializeDeck();
	}

	// Update player list with new Players.
	public static void UpdatePlayers(string[] playerNames)
	{
		for (int i = 0; i < playerNames.Count(); i++)
		{
			// Add new players
			if (i >= Players.Count())
			{
				NewPlayer(playerNames[i], i);
				continue;
			}

			// Change Player Name if it has changed.
			if (Players[i].PlayerName != playerNames[i])
			{
				Players[i].PlayerName = playerNames[i];
			}
		}
	}

	// Add new player to the game
	public static Player NewPlayer(string playerName, int playerNumber)
	{
		Player newPlayer = new Player().Initialize(playerName, playerNumber);
		newPlayer.PlayerName = playerName;

		Players.Add(newPlayer);
		GameManagerInstance.CallDeferred(Node.MethodName.AddChild, newPlayer);

		return newPlayer;
	}

	public static void EndTurn()
	{
		PlayerTurnPhase = TurnPhase.Endstep;

		// Swap to next players turn
		int newPlayerTurn = PlayerTurn < Players.Count() - 1 ? PlayerTurn + 1 : 0;
		PlayerTurn = newPlayerTurn;

		StartUpkeepPhase();
		//GD.Print($"Player Turn: {PlayerTurn}");
	}

	public void StartGame()
	{
		GD.Print($"Game Start!");
		IsGameStarted = true;

		GameManagerInstance.EmitSignal(SignalName.GameStarted);
		StartUpkeepPhase();
	}

	public void EndGame(PCommand command)
	{
		WinningPlayer = Players[command.Kwargs["winningPlayer"].ToInt()];

		IsGameStarted = false;
		PlayerTurnPhase = TurnPhase.GameEnd;

		UI.DisplayWinWindow();
	}

	public static async void StartUpkeepPhase()
	{
		PlayerTurnPhase = TurnPhase.Upkeep;

		ActivePlayer.Upkeep();

		if (ActivePlayer.Tiles.Count() == 0)
		{
			PlayerExpansionType = ExpansionType.Open;
			StartExpansionPhase();
			return;
		}

		int diceRoll = await Roll();

		if (diceRoll == 7)
		{
			PlayerExpansionType = ExpansionType.Open;
		}
		else if (diceRoll % 2 == 0)
		{
			PlayerExpansionType = ExpansionType.LeyLine;
		}
		else
		{
			PlayerExpansionType = ExpansionType.Adjacent;
		}

		GD.Print($"Rolled a {diceRoll} : {PlayerExpansionType}");
		StartExpansionPhase();
	}

	public static void StartExpansionPhase()
	{
		PlayerTurnPhase = TurnPhase.Expansion;
	}

	public static void StartActionPhase()
	{
		PlayerTurnPhase = TurnPhase.Action;
	}


	public static async Task<int> Roll()
	{
		if (!RelayServer.IsActive) return _Roll(7);

		RollCompletionSource = new TaskCompletionSource<bool>();

		if (IsLocalPlayerTurn)
		{
			RelayServer.RpcHost(new PCommand
			{
				ObjectTypeString = typeof(GameManager).FullName,
				MethodStringName = MethodName._ServerRoll
			}
			);
		}

		await RollCompletionSource.Task;
		GD.Print("Roll Received");

		if (CrownBearer != null & CrownWillAvailable) // Have Crownbearer inflict option
		{
			RollCompletionSource = new TaskCompletionSource<bool>();
			
			if (LocalPlayer == CrownBearer) // Only opened by crown
			{
				UI.DisplayCrownWindow(ReplicatedRoll);
			}

			await RollCompletionSource.Task; // Await Crown Decision
		}

		return ReplicatedRoll;
	}

	private static int _Roll(int range)
	{
		return (int)(GD.Randi() % range) + 1;
	}


	/// <summary>
	/// Rolls the die and relays it to all clients. Only to be called on Host
	/// </summary>
	private void _ServerRoll()
	{
		if (RelayServer.LocalPlayerNumber != 0) return;
		
		int roll = _Roll(7);

		RelayServer.RpcClients(new PCommand
		{
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = MethodName._RollReplication,
			Kwargs = new() { ["rollString"] = roll.ToString() }
		});

		ReplicatedRoll = roll;
		RollCompletionSource?.TrySetResult(true);
	}

	/// <summary>
	/// Allows client to receive server roll. Shoud only be called on clients, but not Host
	/// </summary>
	/// <param name="command">command.Kwargs requires { "rollString": int }</param>
	private void _RollReplication(PCommand command)
	{
		ReplicatedRoll = int.Parse(command.Kwargs["rollString"]);
		RollCompletionSource?.TrySetResult(true);
	}

	/// <summary>
	/// Intercepts any players roll and sets the roll to a new value.
	/// </summary>
	/// <param name="command">command.Kwargs { "newRoll": int }</param>
	private static void _CrownRoll(PCommand command)
	{
		int newRollValue = command.Kwargs["newRoll"].ToInt();

		ReplicatedRoll = newRollValue;
		CrownWillAvailable = false;

		RollCompletionSource?.TrySetResult(true);
	}

	/// <summary>
	/// Passes crown influence on roll
	/// </summary>
	private static void _CrownRoll()
	{
		RollCompletionSource?.TrySetResult(true);
	}

	public static void UpdateAllLeyLines()
	{
		foreach (Player player in Players)
		{
			player.BuildLeyLines();
		}
	}

	public static void InitializeDeck()
	{
		GD.Print("Initializing Deck");
		foreach (var pair in GameManagerInstance.DefaultDeck)
		{
			for (int i = 0; i < pair.Value; i++)
			{
				BuildingCard card = pair.Key.Instantiate<BuildingCard>().Initialize();
				Deck.Add(card);
			}
		}

		// Build Lookup for replication
		BuildingCardLookup = new PackedScene[7];
		var BuildingCardIdPairs = GameManagerInstance.DefaultDeck.Keys.Select(k => (k.Instantiate<BuildingCard>().Initialize().ID, k));
		foreach ((int i, PackedScene s) in BuildingCardIdPairs) BuildingCardLookup[i] = s;

		ShuffleDeck();
	}

	public static void ShuffleDeck()
	{
		int count = Deck.Count;

		for (int i = 0; i < count; i++)
		{
			int r = (int)(GD.Randi() % count);
			BuildingCard tmp = Deck[r];
			Deck[r] = Deck[i];
			Deck[i] = tmp;
		}
	}

	public static BuildingCard DrawCard()
	{
		BuildingCard card = Deck[0];
		GD.Print($"Drew Building Card: {card.Text}");
		Deck.RemoveAt(0);

		return card;
	}

	public static async Task StartWar(Player attacker, Player defender)
	{
		PlayerTurnPhase = TurnPhase.War;
		Attacker = attacker;
		Defender = defender;

		WarCompletionSource = new TaskCompletionSource<bool>();

		if (RelayServer.LocalPlayerNumber == 0) // Only run on host
		{
			RelayServer.Rpc(new PCommand {
				ObjectTypeString = typeof(WarWindow).FullName,
				MethodStringName = WarWindow.MethodName.OpenWar,
				Kwargs = {
					["attacker"] = attacker.PlayerNumber.ToString(),
					["defender"] = defender.PlayerNumber.ToString()
				}
			});
		}

		await WarCompletionSource.Task;
	}

	public static void EndWar(PCommand command)
	{
		Attacker.ActiveTroops = command.Kwargs["attackerTroops"].ToInt();
		Defender.ActiveTroops = command.Kwargs["defenderTroops"].ToInt();
		WarCompletionSource.TrySetResult(true);
	}

	public async static void SubscribeToSignal(StringName signalName, Callable callback)
	{
		while (GameManagerInstance == null)
		{
			await Task.Delay(100);
		}

		GameManagerInstance.Connect(signalName, callback);
	}

	public static void StartWar()
	{
		PlayerTurnPhase = TurnPhase.War;
	}

	public static void CheckWinState()
	{
		if (GameManagerInstance == null) return; // Don't run if the game is not initialized

		// Get player with the most points
		Player winningPlayer = Players.MaxBy(p => p.ReputationPoints);

		if (CrownBearer == null) // Crownbearer only starts once one player has 5 points
		{
			if (winningPlayer.ReputationPoints >= MinPointsForCrown)
			{
				winningPlayer.HoldsCrown = true;
				CrownBearer = winningPlayer;
			}
			return;
		}
		else if (winningPlayer.ReputationPoints > CrownBearer.ReputationPoints) // Set new crown bearer only if the crown is usurped
		{
			CrownBearer.HoldsCrown = false;
			winningPlayer.HoldsCrown = true;
			
			CrownBearer = winningPlayer;

			GD.Print($"{CrownBearer.PlayerName} now holds the Crown.");
		}

		// End the game if Crownbearer has the most points, only run on host.
		if (RelayServer.LocalPlayerNumber == 0 & CrownBearer.ReputationPoints >= PointsToWin)
		{
			PCommand command = new() {
					ObjectTypeString = typeof(GameManager).FullName,
					MethodStringName = MethodName.EndGame,
					Kwargs = {
						["winningPlayer"] = winningPlayer.PlayerNumber.ToString()
					}
				};
			if (RelayServer.IsActive)
			{
				RelayServer.Rpc(command);
				return;
			}

			GameManagerInstance.EndGame(command);
		}
	}
}
