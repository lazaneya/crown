
public partial class Fortress : Building, IBuildingWar, IBuildingUpkeep
{
	public Buff GetBuff()
	{
		return new Buff(BuffType.Defense, 2);
	}

	public void Upkeep()
	{
		OwnerPlayer.ArmyPoints += 1;
	}
}
