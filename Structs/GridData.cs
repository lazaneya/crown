
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

public class GridData {
  public Dictionary<string, TileObject> Tiles {get; set;} = new Dictionary<string, TileObject>();
  public float[] Offset {get; set;} = new float[2] {0, 0};

  public GridData(float[] offset, Dictionary<string, TileObject> tiles = null) {
    if (tiles is null) {
      tiles = new Dictionary<string, TileObject>();
    }

    Offset = offset;
    Tiles = tiles;
  }

  // Factories
  public static GridData FromHexGrid(HexGrid hexGrid) {
    GridData gridData = new GridData(new float[2] {hexGrid.Offset[0], hexGrid.Offset[1]});

    foreach (var (coord, tile) in hexGrid.Grid) {
      gridData.Tiles.Add(coord.Id, tile.GetDataObject());
    }

    return gridData;
  }

  public static GridData FromJson(string jsonData) {
    GridData newData = JsonSerializer.Deserialize<GridData>(jsonData);
    UnloadJsonAssembly();

    return newData;
  }

  public static GridData Blank(int size) {
    int centerIndex = size / 2;

    float[] offset = new float[2] {centerIndex, centerIndex};

    GridData gridData = new GridData(offset);
    AxialCoord centerCoord = new AxialCoord((int)offset[0], (int)offset[1]);

    for (int r = 0; r < size; r++) {
			for (int q = 0; q < size; q++) {
				AxialCoord tileCoord = new AxialCoord(q, r);

        // Force grid into hexagon shape
        if (System.Math.Abs(centerCoord.DistanceFrom(tileCoord)) > size / 2) {
          continue;
        }

        gridData.Tiles.Add(tileCoord.Id, new TileObject {
          Type = "ground",
          Resources = new Godot.Collections.Array<string>{"gold", "army", "building"}
        });
			}
		}

    return gridData;
  }


  // Methods
  public string Serialize() {
    JsonSerializerOptions options = new JsonSerializerOptions {WriteIndented = true};

    string jsonOutput = JsonSerializer.Serialize(this, options);
    UnloadJsonAssembly();

    return jsonOutput;
  }

  private static void UnloadJsonAssembly() {
    // Unload Json Serializer Assembly.
    var assembly = typeof(JsonSerializerOptions).Assembly;
    var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
    var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
    clearCacheMethod?.Invoke(null, new object[] {null});
  }
} 