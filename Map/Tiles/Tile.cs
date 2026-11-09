
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[Tool]
public partial class Tile : StaticBody3D
{
	// References
	private MeshInstance3D TileMesh;
	private SpotLight3D Highlight;
	private Node3D CaptureTile;
	private Node3D BuildingContainer;
	private HexGrid Grid;
	public List<Tile> Neighbors { get; private set; } = new();

	// Props
	public AxialCoord Coord { get; private set; }
	[Export] public string Type { get; set; }
	[Export] public bool OpenToLeyLine = true;
	[Export] public bool Capturable = true;
	public Godot.Collections.Dictionary<string, int> Resources { get; set; } = new()
	{
		["reputation"] = 1,
	};

	public Building OwnedBuilding;

	// States
	public bool Active { get; private set; }
	public bool Selectable { get; private set; }
	public Player OwnedBy { get
		{
			return _OwnedBy;	
		}
		private set
		{
			if (_OwnedBy is not null) _OwnedBy.Tiles.Remove(this);
			_OwnedBy = value;
			if (value is not null) value.Tiles.Add(this);
		}
	}

	private Player _OwnedBy;

	// Static Props
	public static Array<string> BasicResourceTypes = new() { "gold", "army", "building" };

	public override void _Ready()
	{
		// Parent Reference
		if (!Engine.IsEditorHint()) Grid = GetParent<HexGrid>();

		// Child references
		TileMesh = GetNode<MeshInstance3D>("TileMesh");
		Highlight = GetNode<SpotLight3D>("SpotLight3D");
		CaptureTile = GetNode<Node3D>("CaptureTile");
		BuildingContainer = GetNode<Node3D>("Building");
	}

	public void GreetNeighbors()
	{
		Neighbors.Clear();

		foreach (AxialCoord co in GetAdjacentCoords())
		{
			if (Grid.Grid.ContainsKey(co))
			{
				Neighbors.Add(Grid.Grid[co]);
			}
		}
	}

	// Static Data
	public virtual Tile Construct(AxialCoord coord, float spacing, TileObject tileObject)
	{
		// Initializes the class
		this.Coord = coord;

		// Init signals
		this.MouseEntered += OnMouseEntered;
		this.MouseExited += OnMouseExited;

		// Tile Position
		Vector2 parsedPosition = Coord.ToPosition(spacing);
		this.Position = new(parsedPosition[0], 0, parsedPosition[1]);

		UpdateCoordLabels();

		return this;
	}

	private void UpdateCoordLabels()
	{
		// Update Debugging Coordinate Labels
		Node3D labels = GetNode<Node3D>("Labels");

		Label3D qNode = labels.GetNode<Label3D>("Q");
		Label3D rNode = labels.GetNode<Label3D>("R");
		Label3D sNode = labels.GetNode<Label3D>("S");

		qNode.Text = Coord.Q.ToString();
		rNode.Text = Coord.R.ToString();
		sNode.Text = Coord.S.ToString();
	}

	public bool Capture(Player player)
	{
		if (player == OwnedBy) return false;

		MeshInstance3D captureInstance = new MeshInstance3D();
		captureInstance.Mesh = player.CaptureMesh;
		captureInstance.MaterialOverride = player.PlayerColorMaterial;

		CaptureTile.CallDeferred("add_child", captureInstance);
		OwnedBy = player;
		SetSelectable(false);

		return true;
	}

	public void NewOwner(Player player)
	{
		OwnedBy = player;

		MeshInstance3D capInstance = CaptureTile.GetChild<MeshInstance3D>(0);
		capInstance.MaterialOverride = player.PlayerColorMaterial;
	}

	public void PlaceBuilding(Building building)
	{
		building.Initialize(OwnedBy, this);
		OwnedBuilding = building;
		BuildingContainer.AddChild(building);
	}

	public void DestroyBuilding()
	{
		if (OwnedBuilding is not null) OwnedBuilding.Destroy();
	}

	private void SetActive(bool active)
	{
		Active = active;
		Highlight.Visible = active;
	}

	public void SetSelectable(bool activeHightlight)
	{
		Selectable = activeHightlight;

		if (Selectable)
			Scale = new Vector3(0.9f, 0.9f, 0.9f);
		else
			Scale = new Vector3(1, 1, 1);
	}

	private void OnMouseEntered()
	{
		if (!GameManager.IsGameStarted) return;

		Grid.ActiveTile = this;
		SetActive(true);
	}

	private void OnMouseExited()
	{
		if (!GameManager.IsGameStarted) return;

		Grid.ActiveTile = null;
		SetActive(false);
	}

	public void PrintState()
	{
		GD.Print($"{Coord.Q}, {Coord.R}, {Coord.S} : Active={Active}");
	}

	public virtual TileObject GetDataObject()
	{
		Array<string> resourceArray = new();

		foreach (KeyValuePair<string, int> r in Resources)
		{
			if (r.Key == "reputation") continue;

			for (int i = 0; i < r.Value; i++)
			{
				resourceArray.Add(r.Key);
			}
		}

		return new TileObject
		{
			Type = Type,
			Resources = resourceArray
		};
	}

	public AxialCoord[] GetAdjacentCoords()
	{
		return AxialCoord.AxialDirectionVectors().Select(c => c + Coord).ToArray();
	}
}
