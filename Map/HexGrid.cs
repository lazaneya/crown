using Godot;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

[Tool]
public partial class HexGrid : Node3D
{
	// References
	[Export] public string TilesDirectory;
	private Dictionary<string, PackedScene> PackedTiles = new Dictionary<string, PackedScene>();

	// Signals
	[Signal]
	public delegate void ActiveTileChangedEventHandler(Tile tile);

	// Props
	public Tile ActiveTile {
		get => _activeTile;
		set
		{
			_activeTile = value;
			EmitSignal(SignalName.ActiveTileChanged, _activeTile);
		}
	}

	// Fields
	private Tile _activeTile;
	private float _spacing;
	private bool _loaded_in_editor = false;
	private Vector2 _offset = new Vector2(0, 0);
	private GridData _loadedGridData;
  public Dictionary<AxialCoord, Tile> Grid {get; private set;} = new Dictionary<AxialCoord, Tile>();

	// Exported Props
	[Export] public float Spacing {
		get {
			return _spacing;
		}
		set {
			_spacing = value;
			if (_loaded_in_editor) this.UpdateGrid();
		}
	}

	[Export] public Vector2 Offset {
		get {
			return _offset;
		}
		set {
			_offset = value;
			if (_loaded_in_editor) this.UpdateGrid();
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_loaded_in_editor = Engine.IsEditorHint();

		// Build the Hexgrid
		LoadPackedTiles();
		LoadGrid();
		BuildGrid();

		GameManager.UpdateAllLeyLines();
	}

	private void LoadPackedTiles()
	{
		PackedTiles.Clear();

		using DirAccess dir = DirAccess.Open(TilesDirectory);

		if (dir is not null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();

			while (fileName != "")
			{
				if (fileName.EndsWith(".remap"))
				{
					fileName = fileName.Replace(".remap", "");
				}

				string filePath = System.IO.Path.Combine(TilesDirectory, fileName);
				PackedScene packedTile = ResourceLoader.Load<PackedScene>(filePath);

				PackedTiles.Add(fileName.Replace("_tile.tscn", ""), packedTile);
				fileName = dir.GetNext();
			}
		}

		GD.Print($"{this.GetPath()}:{PackedTiles.Keys.ToArray().Join(",")}");
	}
	
	private void UpdateGrid() {
		if (!_loaded_in_editor) return;

		ClearGrid();
		BuildGrid();
	}

	public void ReloadGrid() {
		ClearGrid();
		LoadGrid();
		BuildGrid();
	}

	public void ClearGrid() {
		if (Grid is not null) {
			foreach (Tile tile in Grid.Values) {
				tile.Free();
			}
		}

		Grid.Clear();
	}

	// Builds initial hexgrid of tiles
	public void BuildGrid()
	{
		GD.Print("Building Grid");

		if (_loadedGridData is null)
		{
			GD.Print("No Grid Data loaded, load or create a new map");
			return;
		}

		AxialCoord centerCoord = new AxialCoord((int)_offset[0], (int)_offset[1]);

		Vector2 center2d = centerCoord.ToPosition(_spacing);
		this.Position = new(-center2d[0], 0, -center2d[1]);

		foreach (var (coordId, tileObject) in _loadedGridData.Tiles)
		{
			AxialCoord tileCoord = AxialCoord.FromIdString(coordId);

			PackedScene packedTile = PackedTiles[tileObject.Type];

			Tile newTile = (packedTile.Instantiate() as Tile).Construct(tileCoord, _spacing, tileObject);

			Grid.Add(newTile.Coord, newTile);

			AddChild(newTile);
		}

		InitializeTileNeighbors();
	}

	private void InitializeTileNeighbors()
	{
		if (_loaded_in_editor) return;

		foreach (Tile tile in Grid.Values)
		{
			tile.GreetNeighbors();
		}
	}

	public void ReplaceTile(AxialCoord coord, TileObject newTileObject)
	{
		Tile oldTile = Grid[coord];

		PackedScene packedTile = PackedTiles[newTileObject.Type];
		Tile newTile = (packedTile.Instantiate() as Tile).Construct(coord, _spacing, newTileObject);

		oldTile.QueueFree();

		Grid[coord] = newTile;
		AddChild(newTile);

		InitializeTileNeighbors();
		GameManager.UpdateAllLeyLines();
	}

	public void Print() {
		// displays all tile coordinates in the Debugger

		foreach (Tile tile in Grid.Values) {
			Debug.WriteLine(tile.Coord);
		}
	}

	public string ToJson() {
		string gridJson = GridData.FromHexGrid(this).Serialize();

		return gridJson;
	}

	public void LoadGridData(GridData data) {
		_loadedGridData = data;

		// GD.Print(data.Serialize());

		// Initialize Loaded GridData
		_offset = new Vector2(_loadedGridData.Offset[0], _loadedGridData.Offset[1]);
	}

	public void SaveGrid() {
		using FileAccess file = FileAccess.Open("user://grid.json", FileAccess.ModeFlags.Write);
		file.StoreString(this.ToJson());
	}

	public void LoadGrid() {
		using FileAccess file = FileAccess.Open("user://grid.json", FileAccess.ModeFlags.Read);
		LoadGridData(GridData.FromJson(file.GetAsText()));
	}
}
