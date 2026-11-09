using Godot;
using System;

[Tool]
[GlobalClass]
public partial class Building : MeshInstance3D
{
  // Exported Props
  [Export] public BuildingData Data;

  public Player OwnerPlayer;
  public Tile OwnerTile;

  // Should always be set before adding to scene
  public void Initialize(Player player, Tile tile)
  {
    GD.Print("INIT BUILDING");
    OwnerPlayer = player;
    OwnerTile = tile;

    SetSurfaceOverrideMaterial(0, OwnerPlayer.PlayerColorMaterial);

    player.Buildings.Add(this);
  }

  public virtual void Destroy()
  {
    OwnerPlayer.Buildings.Remove(this);
    QueueFree();
  }
}
