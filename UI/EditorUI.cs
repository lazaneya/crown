using Godot;
using System.Linq;

public partial class EditorUI : Control, IGameUI
{
	// Editor References
	private PlayerController playerController;
	private HexGrid Grid;

	// Input Fields
	private int GridSize = 9;
	private string LobbyId = "1";

  public override void _Ready()
  {
    RelayServer.RegisterInstance(this);
  }


	public void Initialize(PlayerController _playerController, HexGrid _hexGrid) {
		playerController = _playerController;
		Grid = _hexGrid;
	}

	public void Update() {
		if (!Visible) return;
	}

	public void OnGridSizeInputChanged(string text) {
		int input;

		if (int.TryParse(text, out input)) {
			GridSize = input;
		}
	}

	public void SelectBuilding(BuildingCard card) {
		(Owner as MainUI).SelectBuilding(card);
	}

	public void SaveGridButtonPressed() {
		GD.Print("Save Grid");
		Grid.SaveGrid();
	}

	public void LoadGridButtonPressed() {
		GD.Print("Load Grid");
		Grid.ReloadGrid();
	}

	public void NewGridButtonPressed() {
		GD.Print("Reset Grid");
		Grid.ClearGrid();
		Grid.LoadGridData(GridData.Blank(GridSize));
		Grid.BuildGrid();
	}

	public void ToggleLeyLinesVisibility()
	{
		Tile[] localPlayerLeyLines = GameManager.LocalPlayer.LeyLines.Values.SelectMany(t => t).ToArray();

		foreach (Tile tile in localPlayerLeyLines)
		{
			tile.SetSelectable(!tile.Selectable);
		}
	}

	private void Yell(string message)
	{
		GD.Print(message);
	}
}
