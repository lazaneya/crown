using Godot;
using System;

public partial class TrainingCamp : Building, IBuildingDiscount
{
	[Export] private string DiscountType = "army";
	[Export] private int Weight = 1;

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
