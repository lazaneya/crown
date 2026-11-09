using Godot;
using System;

public partial class TradeCenter : Building, IBuildingUpkeep
{
  public void Upkeep()
  {
    GD.Print("- TradeCenter: +1 Gold");
    OwnerPlayer.GoldPoints += 1;
  }
}
