using Godot;
using Godot.Collections;
using System;

public partial class Embassy : Building, IBuildingUpkeep
{
	[Export] int MinTilesForReputation = 3;
	public void Upkeep()
	{
		GD.Print($"Embassy");
		Dictionary<Player, int> enemyGoldProduction = new();
		int surroundingTiles = 0;

		foreach (Tile tile in OwnerTile.Neighbors) // Check validity of the embassy upkeep
		{
			if (tile.OwnedBy == OwnerPlayer) return; // Don't allow anyone to get points if the player has a tile owned adjacent to the embassy

			if (tile.OwnedBy != null)
			{
				if (enemyGoldProduction.ContainsKey(tile.OwnedBy))
				{
					enemyGoldProduction[tile.OwnedBy] += 1;
				}
				else
				{
					enemyGoldProduction[tile.OwnedBy] = 1;
				}

				surroundingTiles += 1;
			}
		}

		if (surroundingTiles >= MinTilesForReputation) // Only generates reputation if it hit's the threshold
		{
			GD.Print($"- {OwnerPlayer.PlayerName} Reputation: +1");
			OwnerPlayer.ReputationPoints += 1;
		}

		foreach ((Player player, int count) in enemyGoldProduction) // Always generates gold for nearby enemies
		{
			GD.Print($"- {player.PlayerName} Gold: +{count}");
			player.GoldPoints += count;
		}
	}
}
