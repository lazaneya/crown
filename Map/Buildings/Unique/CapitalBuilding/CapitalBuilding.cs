using Godot;
using System;

public partial class CapitalBuilding : Building, IBuildingDiscount
{
	[Export] private string DiscountType = "reputation";
	[Export] private int Weight = 2;

  public override void _Ready()
  {
		ApplyDiscount();
  }


	public void ApplyDiscount()
	{
		OwnerPlayer.IncrementDiscount(DiscountType, Weight);
	}

	public void RemoveDiscount()
	{
		OwnerPlayer.IncrementDiscount(DiscountType, -Weight);
	}

  public override void Destroy()
  {
		RemoveDiscount();
    base.Destroy();
  }
}
