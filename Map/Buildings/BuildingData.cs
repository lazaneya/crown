using Godot;

[Tool]
[GlobalClass]
public partial class BuildingData : Resource
{
    [Export] public int ID { get; set; }
    [Export] public int Price { get; set; }
    [Export] public string CardText { get; set; } = "Default";
    [Export] public bool BuildsOnEnemy { get; set; } = false;
    [Export] public string Description { get; set; } = "Description Here";
}