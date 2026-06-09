namespace HunterBuddy.Models;

[Serializable]
public class HuntTarget
{
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int TargetQuantity { get; set; } = 10;
    public bool Enabled { get; set; } = true;
}
