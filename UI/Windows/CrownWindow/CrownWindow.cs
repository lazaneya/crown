using Godot;

public partial class CrownWindow : PanelContainer
{
	[Export] Label RollLabel;

	private int Roll = 0;

	public void Display(int roll)
	{
		Visible = true;
		
		Roll = roll;
		RollLabel.Text = roll.ToString();
	}

	private void OnMinus1Pressed()
	{
		Visible = false;

		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = GameManager.MethodName._CrownRoll,
			Kwargs = {
				["newRoll"] = (Roll - 1).ToString()
			}
		});
	}

	private void OnPlus1Pressed()
	{
		Visible = false;

		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = GameManager.MethodName._CrownRoll,
			Kwargs = {
				["newRoll"] = (Roll + 1).ToString()
			}
		});
	}

	private void OnPassButtonPressed()
	{
		Visible = false;

		RelayServer.Rpc(new()
		{
			ObjectTypeString = typeof(GameManager).FullName,
			MethodStringName = GameManager.MethodName._CrownRoll
		});
	}
}
