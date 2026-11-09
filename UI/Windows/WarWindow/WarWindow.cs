using CrownRelayServer;
using Godot;

public partial class WarWindow : PanelContainer, IRelayedObject
{
	public string RelayedID { get; } = "0";

	// Players
	Player Attacker;
	Player Defender;

	// Child references
	[Export] PlayerArmyBets AttackerUI;
	[Export] PlayerArmyBets DefenderUI;

	// Props
	public bool BetsClosed { get; private set; } = false;

	public override void _Ready()
	{
		RelayServer.RegisterInstance(this);
	}

	public void OpenWar(PCommand command)
	{
		GameManager.StartWar();

		int attackerNumber = command.Kwargs["attacker"].ToInt();
		int defenderNumber = command.Kwargs["defender"].ToInt();

		Attacker = GameManager.Players[attackerNumber];
		Defender = GameManager.Players[defenderNumber];

		AttackerUI.Activate(Attacker, true);
		DefenderUI.Activate(Defender, false);

		this.Visible = true;
		(Owner as MainUI).OpenWindows.Add(this);
	}

	/// <summary>
	/// Increments the active troops count by 1 for whichever side is selected
	/// </summary>
	/// <param name="command">command.Kwargs requires { "isAttacker": bool }</param>
	public void IncrementActiveTroops(PCommand command)
	{
		bool isAttacker = bool.Parse(command.Kwargs["isAttacker"]);

		if (isAttacker) AttackerUI.IncrementActiveTroops();
		else DefenderUI.IncrementActiveTroops();

		AttackerUI.OpenBets();
		DefenderUI.OpenBets();
	}

	/// <summary>
	/// Locks in player selected by "isAttacker" Kwarg
	/// </summary>
	/// <param name="command">command.Kwargs requires { "isAttacker": bool }</param>
	public void PlayerLockedIn(PCommand command)
	{
		bool attacker = bool.Parse(command.Kwargs["isAttacker"]);
		if (attacker) AttackerUI.PlayerLockedIn();
		else DefenderUI.PlayerLockedIn();

		CheckBetsClosed();
	}

	public void CloseBets()
	{
		BetsClosed = true;
		CloseWar();
	}

	public void CheckBetsClosed()
	{
		if (AttackerUI.LockedIn && DefenderUI.LockedIn && RelayServer.LocalPlayerNumber == 0)
		{
			GD.Print("Closing Bets");
			RelayServer.Rpc(new PCommand {
				ObjectTypeString = typeof(WarWindow).FullName,
				MethodStringName = MethodName.CloseBets
			});

			RelayServer.Rpc(new() {
				ObjectTypeString = typeof(GameManager).FullName,
				MethodStringName = nameof(GameManager.EndWar),
				Kwargs = {
					["attackerTroops"] = Attacker.ActiveTroops.ToString(),
					["defenderTroops"] = Defender.ActiveTroops.ToString()
				}
			});
		}
	}

	public void CloseWar()
	{
		Visible = false;
		BetsClosed = false;
		(Owner as MainUI).OpenWindows.Remove(this);
	}
}
