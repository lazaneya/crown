using Godot;

[Tool]
public partial class TradeButton : PanelContainer
{
	// Tool Props
	[Export]
	int DefaultPrice
	{
		get => _defaultPrice;
		set
		{
			_defaultPrice = value;
			if (_loaded_in_editor) UpdatePriceLabel();
		}
	}

	[Export]
	public string Resource
	{
		get => _resource;
		set
		{
			_resource = value;
			if (_loaded_in_editor) UpdateResourceLabel();
		}
	}

	[Export]
	Color BackgroundColor
	{
		get => _backgroundColor;
		set
		{
			_backgroundColor = value;
			if (_loaded_in_editor) UpdateBackgroundColor();
		}
	}

	// Tool Fields
	private int _defaultPrice;
	private string _resource;
	private Color _backgroundColor = new Color(.7f, .4f, .4f);
	private bool _loaded_in_editor = false;

	// Props
	public int Price { get { return DefaultPrice - Discount >= 1 ? DefaultPrice - Discount : 1; } }
	public int Discount = 0;

	// Child References
	[Export] private NodePath _goldIconWithPrice;
	private GoldIconWithPrice GoldIconWithPrice;
	[Export] private NodePath _resourceLabel;
	Label ResourceLabel;

	public override void _Ready()
	{
		_loaded_in_editor = Engine.IsEditorHint();

		// init child references
		GoldIconWithPrice = GetNode<GoldIconWithPrice>(_goldIconWithPrice);
		ResourceLabel = GetNode<Label>(_resourceLabel);

		UpdatePriceLabel();
		UpdateResourceLabel();
		UpdateBackgroundColor();
	}

	public void UpdatePriceLabel()
	{
		GoldIconWithPrice.Price = Price;
	}

	public void UpdateResourceLabel()
	{
		ResourceLabel.Text = Resource.Capitalize();
	}

	public void UpdateBackgroundColor()
	{
		StyleBox style = GetThemeStylebox("panel");
		if (style is StyleBoxFlat flat)
		{
			StyleBoxFlat newStyle = (StyleBoxFlat)flat.Duplicate();
			newStyle.BgColor = BackgroundColor;

			AddThemeStyleboxOverride("panel", newStyle);
		}
	}

	public void OnTradeButtonPressed()
	{
		if (GameManager.IsLocalPlayerTurn &
				GameManager.PlayerTurnPhase == TurnPhase.Action &
				GameManager.LocalPlayer.GoldPoints < Price)
			return;

		(Owner as MainUI).playerController.TradeResource(this);
	}
}
