namespace HunterBuddy.Models;

public class DropSource
{
    public uint BNpcNameId { get; set; }
    public string MobName { get; set; } = string.Empty;
    public uint TerritoryId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public List<SpawnPosition> Positions { get; set; } = [];
    public float DropRate { get; set; } = 1f;
}

public class SpawnPosition
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}
