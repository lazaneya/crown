using Godot;
using Godot.Collections;

public partial class PlayerSlate : Panel
{
	[Export] Label PlayerNameLabel;
	[Export] Button RaidButton;
	public Dictionary<string, Label> ResourceCounters { get; private set; } = new();
	[Export] private Dictionary<string, NodePath> ResourceCountersPaths = new();
	private Player _player;

	private bool _hasLocalPlayerStyle = false;

	public override void _Ready()
	{
		foreach ((string counterType, NodePath path) in ResourceCountersPaths)
		{
			ResourceCounters[counterType] = GetNode<Label>(path);
		}
	}

	public void AssignPlayer(Player player)
	{
		_player = player;
		_player.PlayerNameChanged += UpdatePlayerSlateInfo;

		UpdatePlayerSlateInfo();
	}

	public void UpdateCounters()
	{
		if (!IsNodeReady()) return;

		foreach ((string resourceType, int count) in _player.Points)
		{
			ResourceCounters[resourceType].Text = count.ToString();
		}
	}

	public void UpdatePlayerSlateInfo()
	{
		PlayerNameLabel.Text = _player.PlayerName;
		RaidButton.Visible = _player != GameManager.LocalPlayer;

		/*
		if (_player == GameManager.LocalPlayer & !_hasLocalPlayerStyle)
		{
			StyleBox style = GetThemeStylebox("panel");

			if (style is StyleBoxFlat flat)
			{
				StyleBoxFlat newStyle = (StyleBoxFlat)flat.Duplicate();
				newStyle.ExpandMarginLeft = 10;
				newStyle.BorderWidthLeft = 10;

				AddThemeStyleboxOverride("panel", newStyle);
				_hasLocalPlayerStyle = true;
			}
		}
		else if (_hasLocalPlayerStyle)
		{
			GD.Print($"{_player.PlayerName} - Local Player Style Removed");
			RemoveThemeStyleboxOverride("panel");
			_hasLocalPlayerStyle = false;
		}
		*/
	}

	private void RaidButtonPressed()
	{
		if (GameManager.IsLocalPlayerTurn &
				GameManager.PlayerTurnPhase == TurnPhase.Action &
				GameManager.LocalPlayer.RaidablePlayers.Contains(_player))
		{
			GameManager.LocalPlayer.Controller.RaidPlayer(_player);
		}
	}
}
