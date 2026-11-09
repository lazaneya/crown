using System;
using Godot;

public class AxialCoord {

  public int Q {get; private set;}
  public int R {get; private set;}
  public int S {get {return -Q-R;}}

  public int this[int index] {
    get {
      return Getter(index);
    }
  }

  // Id for use of class as key.
  public string Id {get;}

  private int Getter(int index) {
    int[] arr = {Q, R, S};
    return arr[index];
  }

  public AxialCoord(int q, int r) {
    Q = q;
    R = r;
    Id = $"{q}.{r}";
  }

  // Returns Coordinate as X & Z position in space.
	public Vector2 ToPosition(float spacing) {
		Vector2 q = new(spacing * (float)Math.Sin(Math.PI / 6), spacing * (float)Math.Cos(Math.PI / 6));
		Vector2 r = new(spacing,  0);

		return q * Q + r * R;
	}

  public int DistanceFrom(AxialCoord other) {
    AxialCoord vec = other - this;
    return (Math.Abs(vec.Q) + Math.Abs(vec.R) + Math.Abs(vec.S)) / 2;
  }

  // OPERATORS
  
  public static AxialCoord operator -(AxialCoord a, AxialCoord b) {
    return new AxialCoord(a.Q - b.Q, a.R - b.R);
  }

  public static AxialCoord operator +(AxialCoord a, AxialCoord b)
  {
    return new AxialCoord(a.Q + b.Q, a.R + b.R);
  }

	public static AxialCoord[] AxialDirectionVectors()
	{
		return new AxialCoord[] {
			new AxialCoord(1, 0), new AxialCoord(1, -1),
			new AxialCoord(0, 1), new AxialCoord(-1, 1),
			new AxialCoord(-1, 0), new AxialCoord(0, -1)
		};
	}

  public override string ToString()
  {
    return $"[{Q}, {R}, {S}]";
  }

  public override int GetHashCode()
  {
      return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
  }

  public override bool Equals(object obj)
  {
      if (obj == null) return false;
      if (!(obj is AxialCoord)) return false;

      return StringComparer.OrdinalIgnoreCase.Equals(this.Id, ((AxialCoord)obj).Id);
  }

  // STATIC FUNCTIONS

  public static AxialCoord FromVector3(Vector3 v) {
    return new AxialCoord((int)v[0], (int)v[1]);
  }

  public static AxialCoord FromIdString(string id) {
    int[] parsedId = Array.ConvertAll(id.Split("."), int.Parse);

    return new AxialCoord(parsedId[0], parsedId[1]);
  }
}