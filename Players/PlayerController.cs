using System.Data;
using System.Runtime.Serialization;
using CrownRelayServer;
using Godot;
using Godot.Collections;

public partial class PlayerController : Node3D, IRelayedObject {
	// References
	[Export] public HexGrid hexGrid;
	[Export] public MainUI UI;
	[Export] public Camera3D Camera;

	// Building Placement
	public BuildingCard SelectedBuildingCard;

	// Camera Movement
	[Export] public float CameraSpeed = 1f;
	[Export] public float CameraPadding = 10;
	private Vector2 Velocity = new(0, 0);
	private Vector2 ViewportSize;

	// Relayed Object ID
	public string RelayedID { get; } = "0";

	public override void _Ready()
	{
		RelayServer.RegisterInstance(this);

		Input.MouseMode = Input.MouseModeEnum.Visible;

		ViewportSize = GetViewport().GetVisibleRect().Size;
		GetViewport().SizeChanged += HandleViewportSizeChanged;
	}

	private void HandleViewportSizeChanged() {
		ViewportSize = GetViewport().GetVisibleRect().Size;
	}

	private void HandlePlayerMovement() {
		Vector2 mousePos = GetViewport().GetMousePosition();


		if (mousePos[0] <= CameraPadding) {
			Velocity[0] = -1; // Screen Left
		} else if (mousePos[0] >= ViewportSize[0] - CameraPadding) {
			Velocity[0] = 1; // Screen Right
		} else {
			Velocity[0] = 0;
		}

		if (mousePos[1] <= CameraPadding) {
			Velocity[1] = -1; // Screen Up
		} else if (mousePos[1] >= ViewportSize[1] - CameraPadding) {
			Velocity[1] = 1; // Screen  Down
		} else {
			Velocity[1] = 0;
		}
	}

	public override void _Process(double delta)
	{
		UpdatePosition((float)delta);
	}

	public override void _Input(InputEvent @event) {
		// Handle Player Input

		// Mouse Click
		if (@event is InputEventMouseButton eventMouseButton) {
			if (eventMouseButton.IsPressed()) handleMousePressed(eventMouseButton);
		}

		// Mouse Movement
		if (@event is InputEventMouseMotion)
		{
			return;
			if (GameManager.IsGameStarted) HandlePlayerMovement();
		}
		
		// Keyboard Input
		if (@event is InputEventKey eventKey)
		{
			if (eventKey.IsPressed()) handleButtonPressed(eventKey);
		}
	}

	private void handleMousePressed(InputEventMouseButton eventMouseButton) {
		Tile activeTile = hexGrid.ActiveTile;
		if (activeTile == null || !GameManager.IsGameStarted || UI.HasPriority)
		{
			SelectedBuildingCard = null;
			return;
		}

		switch (eventMouseButton.ButtonIndex)
		{
			case MouseButton.Left: // Capture Tile or build building
				if (GameManager.IsLocalPlayerTurn &
						SelectedBuildingCard != null &
						GameManager.PlayerTurnPhase == TurnPhase.Action)
				{
					if (SelectedBuildingCard.IsValidTilePlacement(activeTile, GameManager.LocalPlayer)) PlaceBuilding(activeTile);
					break;
				}

				if (GameManager.IsLocalPlayerTurn &
						GameManager.PlayerTurnPhase == TurnPhase.Action &
						activeTile.OwnedBy != null &
						activeTile.OwnedBy != GameManager.LocalPlayer &
						GameManager.LocalPlayer.AttackableTiles.Contains(activeTile))
				{
					AttackTile(activeTile);
					break;
				}

				if (GameManager.IsLocalPlayerTurn &
					 	GameManager.PlayerTurnPhase == TurnPhase.Expansion &
						activeTile.OwnedBy == null)
				{
					CaptureTile(activeTile);
					break;
				}

				SelectedBuildingCard = null;
				break;
		}

		if (UI.EditorEnabled) handleEditorMousePresses(eventMouseButton, activeTile);
	}

	private void PlaceBuilding(Tile activeTile)
	{
		GD.Print(SelectedBuildingCard.Mint, SelectedBuildingCard.Text);

		PCommand command = new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.PlaceBuilding,
			Kwargs = {
				["id"] = activeTile.Coord.Id,
				["mint"] = SelectedBuildingCard.Mint.ToString()
			}
		};

		if (RelayServer.IsActive)
		{
			RelayServer.Rpc(command);
			return;
		}

		GameManager.LocalPlayer.PlaceBuilding(command);
	}

	private void CaptureTile(Tile activeTile) {
		PCommand command = new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.CaptureTile,
			Kwargs = {
				["id"] = activeTile.Coord.Id
			}
		};

		if (RelayServer.IsActive)
		{
			RelayServer.Rpc(command);
			return;
		}

		GameManager.LocalPlayer.CaptureTile(command);
	}

	private void AttackTile(Tile activeTile)
	{
		PCommand command = new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.AttackTile,
			Kwargs = {
				["id"] = activeTile.Coord.Id
			}
		};

		if (RelayServer.IsActive)
		{
			RelayServer.Rpc(command);
			return;
		}

		GameManager.LocalPlayer.CaptureTile(command);
	}

	public void DrawCard()
	{
		PCommand command = new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.DrawCard
		};
		
		if (RelayServer.IsActive)
		{
			RelayServer.RpcHost(command);
			return;
		}

		GameManager.LocalPlayer.DrawCard();
	}

	public void RaidPlayer(Player player)
	{
		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.RaidPlayer,
			Kwargs = {
				["target"] = player.PlayerNumber.ToString()
			}
		});
	}

	public void TradeResource(TradeButton tradeButton)
	{
		PCommand command = new()
		{
			ObjectTypeString = typeof(Player).FullName,
			ObjectID = GameManager.LocalPlayer.RelayedID,
			MethodStringName = Player.MethodName.TradeResource,
			Kwargs = {
				["resource"] = tradeButton.Resource,
				["price"] = tradeButton.Price.ToString()
			}
		};

		if (RelayServer.IsActive)
		{
			RelayServer.Rpc(command);
			return;
		}

		GameManager.LocalPlayer.TradeResource(command);
	}

	public void SkipTurn()
	{
		PCommand command = new()
		{
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = GameManager.MethodName.EndTurn
		};

		if (RelayServer.IsActive)
		{
			RelayServer.Rpc(command);
			return;
		}

		GameManager.EndTurn();
	}

	private void handleEditorMousePresses(InputEventMouseButton eventMouseButton, Tile activeTile)
	{
		switch (eventMouseButton.ButtonIndex)
		{
			case MouseButton.Right: // Swap Tile Type
				hexGrid.ReplaceTile(activeTile.Coord, new TileObject
				{
					Type = activeTile.Type == "ground" ? "water" : "ground",
					Resources = activeTile.Type == "ground" ? null : new Array<string> { Tile.BasicResourceTypes.PickRandom(),
																																							 Tile.BasicResourceTypes.PickRandom(),
																																							 Tile.BasicResourceTypes.PickRandom()
					}
				});

				break;
		}
	}

	private void handleButtonPressed(InputEventKey eventKey) {
		// E : Editor Toggle
		if (eventKey.Keycode == Key.KpAdd & GameManager.IsGameStarted) {
			this.UI.EditorEnabled = !UI.EditorEnabled;
		}

		// Esc : Close Program
		if (eventKey.Keycode == Key.Escape) {
			GetTree().Quit();
		}

		if (UI.EditorEnabled) handleEditorButtonPresses(eventKey);
	}

	private void handleEditorButtonPresses(InputEventKey eventKey) {
		// Empty for now
	}

	private void UpdatePosition(float delta) {
		Position += new Vector3(Velocity[0] * CameraSpeed, 0, Velocity[1] * CameraSpeed) * delta;
	}
  
}