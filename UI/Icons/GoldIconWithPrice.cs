using Godot;
using System;

[Tool]
public partial class GoldIconWithPrice : MarginContainer
{
	// Child references
	private Label PriceLabel;
	[Export] private NodePath _priceLabel;

	// Props
	[Export] public int Price
	{
		get => _price; set
		{
			_price = value;
			if (_is_loaded) PriceLabel.Text = _price.ToString();
		}
	}

	// Fields
	private int _price = 0;
	private bool _is_loaded = false;

	public override void _Ready()
	{
		PriceLabel = GetNode<Label>(_priceLabel);
		PriceLabel.Text = Price.ToString();

		_is_loaded = true;
	}
}
