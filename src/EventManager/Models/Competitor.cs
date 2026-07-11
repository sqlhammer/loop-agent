namespace EventManager;

public class Competitor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StylesJson { get; set; } = "[]";
    public string Birthdate { get; set; } = string.Empty;
    public double LastWeighInWeight { get; set; }
    public string LastWeighInUnits { get; set; } = string.Empty;
}
