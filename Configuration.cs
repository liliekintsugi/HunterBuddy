using Dalamud.Configuration;
using HunterBuddy.Models;

namespace HunterBuddy;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<HuntTarget> HuntTargets { get; set; } = [];
    public float NavigationTolerance { get; set; } = 5f;
    public bool UseRotationSolver { get; set; } = true;
    public int LootWaitMs { get; set; } = 2000;
    public int MobSearchTimeoutSec { get; set; } = 15;
    public int SelectedGearSetId { get; set; } = -1; // -1 = ne pas changer de classe

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
