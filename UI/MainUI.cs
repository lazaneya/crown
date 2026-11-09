
using System.Linq;
using CrownRelayServer;
using Godot;
using Godot.Collections;


public partial class MainUI : CanvasLayer, IRelayedObject, IGameUI
{
	// Game references
	[Export] public PlayerController playerController;
	[Export] public HexGrid hexGrid;

	// UI references
	[Export] private EditorUI editorUI;
	[Export] private Control GameUI;
	[Export] private Container TradeWindow;
	[Export] private WinWindow WinWindow;
	[Export] private CrownWindow CrownWindow;

	// Relayed Object Info
	public string RelayedID { get; } = "0";

	// Editor Controls
	public bool EditorEnabled
	{
		get => _editorEnabled;
		set
		{
			_editorEnabled = value;
			editorUI.Visible = value;
			GameUI.Visible = !value;
		}
	}
	private bool _editorEnabled;

	// Main Menu
	[Export] private Container MainMenuContainer;
	[Export] private LineEdit LobbyIdLineEdit;
	[Export] private LineEdit PlayerNameLineEdit;

	// Main UI Wrapper references
	[Export] private Container TurnTracker;
	[Export] private Label PlayerName;
	[Export] private Button SkipTurnButton;
	[Export] private Button StartGameButton;
	[Export] private Button DrawCardButton;
	[Export] private TileInfoPopupPanel TileInfoPopup;

	// Building Cards
	[Export] private Container BuildingCardContainer;
	private BuildingCard _activeBuildingCard;

	// Player UI Info
	[Export] PackedScene PlayerSlatePackedScene;
	[Export] Container PlayersWrapper;

	// War UI Info
	[Export] public WarWindow WarWindowUI;

	// General Props
	public Array<Control> OpenWindows = new();
	public bool HasPriority { get { return OpenWindows.Count > 0; } }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		TurnTracker.Visible = false;
		PlayerName.Visible = false;
		PlayersWrapper.Visible = false;
		TradeWindow.Visible = false;
		DrawCardButton.Visible = false;
		SkipTurnButton.Visible = false;

		// Signals
		GameManager.SubscribeToSignal(GameManager.SignalName.PlayerTurnPhaseChanged,
																	new Callable(this, nameof(PlayerTurnPhaseChangedEventHandler)));

		GameManager.SubscribeToSignal(GameManager.SignalName.GameStarted,
																	new Callable(this, nameof(GameStartedEventHandler)));

		hexGrid.ActiveTileChanged += UpdateTileInfoPopup;

		editorUI.Initialize(playerController, hexGrid);
		editorUI.Visible = EditorEnabled;

		Update();
	}

	public void Update()
	{
		editorUI.Update();
		UpdatePlayerName();
		UpdateResourceCounters();
		UpdateTradeWindow();

		if (RelayServer.IsActive)
		{
			UpdateTurnTracker();
		}
	}

	private void UpdatePlayerName()
	{
		PlayerName.Text = GameManager.LocalPlayer.PlayerName;
	}

	private void UpdateResourceCounters()
	{
		foreach (PlayerSlate slate in PlayersWrapper.GetChildren())
		{
			slate.UpdateCounters();
		}
	}

	public void UpdateTurnTracker()
	{
		TurnTracker.GetChild<Label>(0).Visible = GameManager.PlayerTurn == RelayServer.LocalPlayerNumber;
		TurnTracker.GetChild<Label>(1).Text = $"{GameManager.PlayerTurnPhase}" +
																						(GameManager.PlayerTurnPhase == TurnPhase.Expansion ? $" - {GameManager.PlayerExpansionType}" : "");
	}

	public void UpdateTradeWindow()
	{
		Array<TradeButton> tradeButtons = new Array<TradeButton>(GetTree().GetNodesInGroup("TradeButton").Select(b => b as TradeButton));

		foreach (TradeButton tradeButton in tradeButtons)
		{
			tradeButton.Discount = GameManager.LocalPlayer.Discounts[tradeButton.Resource];
			tradeButton.UpdatePriceLabel();
		}
	}

	private void HostServerButtonPressed()
	{
		MainMenuContainer.Visible = false;
		GameManager.LocalPlayer.PlayerName = PlayerNameLineEdit.Text;
		RelayServer.HostServer();
	}

	private void JoinServerButtonPressed()
	{
		MainMenuContainer.Visible = false;
		GameManager.LocalPlayer.PlayerName = PlayerNameLineEdit.Text;
		RelayServer.JoinServer(LobbyIdLineEdit.Text.ToInt());
	}

	private void EditorModeButtonPressed()
	{
		MainMenuContainer.Visible = false;
		EditorEnabled = true;
		GameManager.GameManagerInstance.StartGame();
	}

	private void DrawCardButtonPressed()
	{
		if (GameManager.IsLocalPlayerTurn &
				GameManager.PlayerTurnPhase == TurnPhase.Action &
				GameManager.LocalPlayer.Hand.Count() < GameManager.MaxHandSize &
				GameManager.LocalPlayer.BuildingPoints >= GameManager.DefaultBuildingCost)
		{
			playerController.DrawCard();
		}
	}

	private void SkipTurnButtonPressed()
	{
		if (GameManager.IsLocalPlayerTurn &
				GameManager.PlayerTurnPhase == TurnPhase.Action)
		{
			playerController.SkipTurn();
		}
	}

	private void StartGameButtonPressed()
	{
		StartGameButton.Visible = false;

		GD.Print(GameManager.GameManagerInstance.GetType().FullName);

		RelayServer.Rpc(new PCommand {
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = GameManager.MethodName.StartGame
		});
	}

	public void DisplayHostControls()
	{
		StartGameButton.Visible = true;
	}

	public void DisplayCrownWindow(int roll)
	{
		CrownWindow.Display(roll);
	}

	public void AddCardToHand(BuildingCard card)
	{
		BuildingCardContainer.AddChild(card);
		card.Owner = this;
	}

	public void SelectBuilding(BuildingCard card)
	{
		playerController.SelectedBuildingCard = card;
		GD.Print($"Selected Building: {playerController.SelectedBuildingCard.Text}");
	}

	public void DestroyActiveBuildingCard()
	{
		if (playerController.SelectedBuildingCard is not null) playerController.SelectedBuildingCard.Destroy();
		playerController.SelectedBuildingCard = null;
	}

	public void AddPlayerSlate(Player player)
	{
		PlayerSlate NewPlayerSlate = PlayerSlatePackedScene.Instantiate<PlayerSlate>();
		NewPlayerSlate.AssignPlayer(player);
		NewPlayerSlate.UpdateCounters();

		PlayersWrapper.AddChild(NewPlayerSlate);
	}

	public void HideWarWindow()
	{
		RelayServer.Rpc(new PCommand
		{
			ObjectTypeString = WarWindowUI.GetType().FullName,
			MethodStringName = WarWindow.MethodName.CloseWar
		});
	}

	public void DisplayWinWindow()
	{
		WinWindow.WinningPlayerLabel.Text = $"Winner: {GameManager.WinningPlayer.PlayerName}";
		WinWindow.Visible = true;
	}

	public void DisplayLobby()
	{
		GD.Print("Display Lobby");
		PlayersWrapper.Visible = true;
	}

	private void PlayerTurnPhaseChangedEventHandler()
	{
		if (!GameManager.IsLocalPlayerTurn)
		{
			DefaultUIVisibility();
			return;
		}

		switch (GameManager.PlayerTurnPhase)
		{
			case TurnPhase.Action:
				TradeWindow.Visible = true;
				SkipTurnButton.Visible = true;
				DrawCardButton.Visible = true;
				break;
			default:
				DefaultUIVisibility();
				break;
		}
	}

	private void GameStartedEventHandler()
	{
		TurnTracker.Visible = true;
		PlayerName.Visible = true;
		BuildingCardContainer.Visible = true;
	}

	private void DefaultUIVisibility()
	{
		TradeWindow.Visible = false;
		SkipTurnButton.Visible = false;
		DrawCardButton.Visible = false;
	}

	private void UpdateTileInfoPopup(Tile tile)
	{
		if (tile is null)
		{
			TileInfoPopup.Hide();
			return;
		}

		Vector3 tilePos = tile.GlobalPosition;
		Vector2 viewportPos = playerController.Camera.UnprojectPosition(tilePos);

		TileInfoPopup.SetActiveTile(tile, viewportPos);
	}
}
