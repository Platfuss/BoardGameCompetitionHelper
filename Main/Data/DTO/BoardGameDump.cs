namespace Main.Data.DTO;

public class BoardGameDump
{
    public int Id { get; set; }
    public int? LanguageId { get; set; }
    public string PrimaryName { get; set; }
    public string? SecondayFullName { get; set; }
    public List<string> Names { get; set; } = new();

    public BoardGameDump(int id, string primaryName)
    {
        Id = id;
        PrimaryName = primaryName;
    }
}
