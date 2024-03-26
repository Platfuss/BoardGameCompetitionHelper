namespace Main.Data;

public class FoundItem
{
    public int BggId { get; set; }
    public (int X, int Y) StartingPosition { get; set; }
    public int Length { get; set; }
    public Directions Direction { get; set; }
}

public enum Directions
{
    North, East, South, West, NorthEast, NorthWest, SouthEast, SouthWest
}
