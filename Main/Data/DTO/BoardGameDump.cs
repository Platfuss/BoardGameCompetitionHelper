namespace Main.Data.DTO;

public class BoardGameDump
{
    public int Id { get; set; }
    public string PrimaryName { get; set; }
    public List<string> Names { get; set; } = null!;

    public BoardGameDump(int id, string primaryName)
    {
        Id = id;
        PrimaryName = primaryName;
    }
}
