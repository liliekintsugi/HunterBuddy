using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace HunterBuddy.Windows;

public class ConfigWindow : Window
{
    public ConfigWindow() : base("HunterBuddy — Configuration##cfg")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 240),
            MaximumSize = new Vector2(520, 420)
        };
    }

    public override void Draw()
    {
        var cfg     = Plugin.Config;
        var changed = false;

        ImGui.TextUnformatted("Navigation");
        ImGui.Separator();

        var tol = cfg.NavigationTolerance;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderFloat("Tolérance d'arrivée (yalms)##tol", ref tol, 1f, 20f))
        { cfg.NavigationTolerance = tol; changed = true; }

        var timeout = cfg.MobSearchTimeoutSec;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("Timeout recherche mob (s)##timeout", ref timeout, 5, 60))
        { cfg.MobSearchTimeoutSec = timeout; changed = true; }

        ImGui.Spacing();
        ImGui.TextUnformatted("Combat");
        ImGui.Separator();

        var rsr = cfg.UseRotationSolver;
        if (ImGui.Checkbox("Activer RotationSolverReborn automatiquement##rsr", ref rsr))
        { cfg.UseRotationSolver = rsr; changed = true; }
        ImGui.TextDisabled("  Désactive après chaque combat.");

        var loot = cfg.LootWaitMs;
        ImGui.SetNextItemWidth(160);
        if (ImGui.SliderInt("Délai pillage (ms)##loot", ref loot, 500, 5000))
        { cfg.LootWaitMs = loot; changed = true; }

        if (changed) cfg.Save();
    }
}
