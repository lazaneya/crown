
public class Buff
{
  public BuffType Type;
  public int Scalar;

  public Buff(BuffType type, int scalar)
  {
    Type = type;
    Scalar = scalar;
  }
}

public enum BuffType
{
  Offense,
  Defense
}