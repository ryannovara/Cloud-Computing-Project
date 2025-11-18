namespace Company.Function.Models;

public class Game
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Upc { get; set; } = string.Empty;
    public string? Data { get; set; } // Store full JSON if needed
    public int? Year { get; set; } // Release year
    public string? Publisher { get; set; }
}

