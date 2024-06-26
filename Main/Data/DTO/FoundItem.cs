﻿namespace Main.Data.DTO;

public class FoundItem
{
    public int BggId { get; set; }
    public string Name { get; set; } = "";
    public (int? Id, string? Value) LanguageInfo { get; set; }
    public (int X, int Y) StartingPosition { get; set; }
    public int Length => Name.Length;
    public Directions Direction { get; set; }
}
