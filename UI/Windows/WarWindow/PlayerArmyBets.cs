using Godot;
using System;

public partial class PlayerArmyBets : HBoxContainer
{
	// Owner
	WarWindow War;

	// Child References
	[Export] Label ActiveTroopsLabel;
	[Export] Button IncrementTroopsButton;
	[Export] Label TotalTroopsLabel;
	[Export] Button ConfirmationButton;


	// Fields
	private Player _player;
	public bool LockedIn = false;
	public bool Attacker;

	public override void _Ready()
	{
		War = Owner as WarWindow;

		IncrementTroopsButton.Pressed += HandleIncrementTroopsButtonPressed;
		ConfirmationButton.Pressed += HandleConfirmationButtonPressed;
	}

	public void Activate(Player player, bool attacker)
	{
		this._player = player;
		Attacker = attacker;
		TotalTroopsLabel.Text = $"Max Army: {player.ArmyPoints}";
		Reset();
	}

	public void Reset()
	{
		_player.ActiveTroops = 0;
		OpenBets();

		Update();
		if (_player.ActiveTroops >= _player.ArmyPoints) HandleConfirmationButtonPressed();

	}

	public void Update()
	{
		ActiveTroopsLabel.Text = _player.ActiveTroops.ToString();
	}

	public void HandleIncrementTroopsButtonPressed()
	{
		if (War.BetsClosed) return;

		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(WarWindow).FullName,
			ObjectID = War.RelayedID,
			MethodStringName = WarWindow.MethodName.IncrementActiveTroops,
			Kwargs = {
				["isAttacker"] = Attacker.ToString()
			}
		});
	}

	public void IncrementActiveTroops()
	{
		if (_player == GameManager.LocalPlayer) ConfirmationButton.Visible = true;

		// Increment only to max
		_player.ActiveTroops = _player.ActiveTroops < _player.ArmyPoints ? _player.ActiveTroops + 1 : _player.ActiveTroops;

		if (_player.ActiveTroops == _player.ArmyPoints)
		{
			HandleConfirmationButtonPressed();
		}

		Update();
	}

	public void OpenBets()
	{
		LockedIn = false;

		bool IsLocalPlayerUI = _player == GameManager.LocalPlayer;

		// Show buttons if local player owns ui
		ConfirmationButton.Visible = IsLocalPlayerUI;
		IncrementTroopsButton.Visible = IsLocalPlayerUI;
	}

	public void HandleConfirmationButtonPressed()
	{
		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(WarWindow).FullName,
			ObjectID = War.RelayedID,
			MethodStringName = WarWindow.MethodName.PlayerLockedIn,
			Kwargs = {
				["isAttacker"] = Attacker.ToString()
			}
		});
	}

	public void PlayerLockedIn()
	{
		LockedIn = true;
		ConfirmationButton.Visible = false;
		IncrementTroopsButton.Visible = false;
	}
}
