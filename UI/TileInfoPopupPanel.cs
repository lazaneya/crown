using Godot;

public partial class TileInfoPopupPanel : PanelContainer
{
	// child references
	[Export] private Label BuildingNameLabel;
	[Export] private RichTextLabel DescriptionLabel;

	// props
	BuildingData BuildingData;

	public void SetActiveTile(Tile tile, Vector2 viewportPos)
	{
		if (tile.OwnedBuilding is null) return;
		BuildingData = tile.OwnedBuilding.Data;

		// Update panel info
		BuildingNameLabel.Text = BuildingData.CardText;
		DescriptionLabel.Text = $"[center]{BuildingData.Description}";

		// Update position of label;
		Visible = true;
		Position = viewportPos - (Size / 2);
	}
}
