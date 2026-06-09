using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace HunterBuddy.Windows;

public class ConfigWindow : Window
{
    public ConfigWindow() : base("HunterBuddy — Configuration##cfg")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 280),
            MaximumSize = new Vector2(560, 480)
        };
    }

    public override void Draw()
    {
        var cfg     = Plugin.Config;
        var changed = false;

        // ── Classe ────────────────────────────────────────────────────────────
        ImGui.TextUnformatted("Classe");
        ImGui.Separator();
        changed |= DrawJobPicker(cfg);
        ImGui.TextDisabled("  Changement automatique au démarrage du farm.");

        ImGui.Spacing();

        // ── Navigation ────────────────────────────────────────────────────────
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

        // ── Combat ────────────────────────────────────────────────────────────
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

    private static unsafe bool DrawJobPicker(Configuration cfg)
    {
        var gsm = RaptureGearsetModule.Instance();
        if (gsm == null)
        {
            ImGui.TextDisabled("Gear sets indisponibles.");
            return false;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<ClassJob>();

        // Construire la liste : (-1, "Aucun") + gear sets dispo
        var ids    = new List<int>   { -1 };
        var labels = new List<string>{ "Aucun (ne pas changer)" };

        for (var i = 0; i < 100; i++)
        {
            var entry = gsm->GetGearset(i);
            if (entry == null || !entry->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))
                continue;
            var abbr = sheet?.GetRow(entry->ClassJob).Abbreviation.ExtractText();
            if (string.IsNullOrEmpty(abbr)) abbr = $"Job{entry->ClassJob}";
            ids.Add(i);
            labels.Add($"[{i + 1}] {abbr}");
        }

        var currentIdx = ids.IndexOf(cfg.SelectedGearSetId);
        if (currentIdx < 0) currentIdx = 0;

        ImGui.SetNextItemWidth(220);
        var changed = false;
        if (ImGui.BeginCombo("Classe##jobpicker", labels[currentIdx]))
        {
            for (var i = 0; i < labels.Count; i++)
            {
                var selected = i == currentIdx;
                if (ImGui.Selectable(labels[i], selected))
                {
                    cfg.SelectedGearSetId = ids[i];
                    changed = true;
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        return changed;
    }
}
