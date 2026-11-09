using Godot;

public partial class WatchTower : Building, IBuildingWar
{
	[Export] int AdjacentTileOffensiveBuff = 2;
	public Buff GetBuff()
	{
		return new Buff(BuffType.Offense, AdjacentTileOffensiveBuff);
	}
}
