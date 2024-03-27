using System.Text;

namespace Main.Data;

public static class DirectionsChecker
{
    public static IEnumerable<string> GetWords(Directions direction, char[][] letterArray, int i, int j)
    {
        StringBuilder builder = new();
        builder.Append(letterArray[i][j]);

        while (true)
        {
            switch (direction)
            {
                case Directions.North: i--; break;
                case Directions.East: j++; break;
                case Directions.South: i++; break;
                case Directions.West: j--; break;
                case Directions.NorthEast: i--; j++; break;
                case Directions.NorthWest: i--; j--; break;
                case Directions.SouthEast: i++; j++; break;
                case Directions.SouthWest: i++; j--; break;
                default: throw new NotImplementedException($"Unknown Direction: {direction}");
            }

            if (IsValidElement(letterArray, i, j))
                builder.Append(letterArray[i][j]);
            else
                yield break;

            yield return builder.ToString();
        }
    }

    private static bool IsValidElement(char[][] letterArray, int i, int j)
    {
        return letterArray.ElementAtOrDefault(i) != default
            && letterArray[i]?.ElementAtOrDefault(j) != 0;
    }
}

public enum Directions
{
    North, East, South, West, NorthEast, NorthWest, SouthEast, SouthWest
}
