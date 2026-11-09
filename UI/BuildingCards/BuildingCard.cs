
using Godot;

[Tool]
public partial class BuildingCard : Container
{
	// Building Info
	[Export] public PackedScene BuildingScene;
	[Export] private BuildingData Data;

	// Child References
	[Export] private Label CardTextLabel;
	[Export] private GoldIconWithPrice GoldIcon;
	[Export] private RichTextLabel DescriptionLabel;


	// Inherited Building Props
	public int Price { get => Data.Price; }
	public string Text { get => Data.CardText; }
	public int ID { get => Data.ID; }
	public bool BuildsOnEnemy { get => Data.BuildsOnEnemy; }
	public string Description { get => Data.Description; }

	// fields
	private static int MasterMint = 0;
	public int Mint { get; private set; }
	private bool _loaded_in_editor = false;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			Initialize();
			_loaded_in_editor = true;
		}
	}

	public void Display()
	{
		CardTextLabel.Text = Text;
		GoldIcon.Price = Price;
		DescriptionLabel.Text = Description;
	}


	// Should be called to properly initialize every single Building Card;
	public BuildingCard Initialize(int mint = -1)
	{
		MintCard(mint);
		Display();

		return this;
	}

	// Mint needed for proper Id with replication
	private void MintCard(int mint)
	{
		if (mint != -1)
		{
			Mint = mint;
			return;
		}

		Mint = MasterMint;
		MasterMint += 1;
	}

	public bool IsValidTilePlacement(Tile tile, Player player)
	{
		if (BuildsOnEnemy & tile.OwnedBy != player & tile.OwnedBy != null) // Check if tile should be placed on enemy lines
		{
			return true;
		}

		if (!BuildsOnEnemy & tile.OwnedBy == player) // Check if tile should be placed on friendly lines
		{
			return true;
		}

		return false;
	}

	public void OnPressed()
	{
		IGameUI ui = Owner as IGameUI;

		ui.SelectBuilding(this);
	}

	public void Destroy()
	{
		GD.Print($"Destroy Active Building Card {Mint}, {CardTextLabel}");
		QueueFree();
	}

	private void OnMouseEnter()
	{
		DescriptionLabel.Visible = true;
	}

	private void OnMouseExit()
	{
		DescriptionLabel.Visible = false;
	}
}
