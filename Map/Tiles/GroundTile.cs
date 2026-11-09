
using Godot;

[Tool]
public partial class GroundTile : Tile {
  // References
  [Export] private Node3D TileResourcesContainer;

  // Props
  public new string Type { get; } = "ground";

  public override Tile Construct(AxialCoord coord, float spacing, TileObject tileObject)
  {
    base.Construct(coord, spacing, tileObject);
    
    foreach (string resourceName in tileObject.Resources)
    {
      if (Resources.ContainsKey(resourceName))
      {
        Resources[resourceName] += 1;
      }
      else
      {
        Resources[resourceName] = 1;
      }
    }

    RenderResources();

    return this;
  }

  public void RenderResources() {
    int i = 0;
    foreach (string resourceName in GetDataObject().Resources) {
      PackedScene resourceScene = ResourceLoader.Load<PackedScene>($"res://Map/Resources/Unique/{resourceName}_resource.tscn");
      TileResourcesContainer.GetChild(i).AddChild(resourceScene.Instantiate());

      i++;
    }
  }
}