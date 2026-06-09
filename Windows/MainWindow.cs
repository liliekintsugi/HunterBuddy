using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using HunterBuddy.Models;
using HunterBuddy.Services;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace HunterBuddy.Windows;

public class MainWindow : Window
{
    private string _searchQuery = string.Empty;
    private List<(uint Id, string Name)> _searchResults = [];
    private int _selectedResult = -1;
    private int _addQuantity = 10;
    private bool _showAddPanel;

    public MainWindow() : base("HunterBuddy", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 340),
            MaximumSize = new Vector2(900, 700)
        };
    }

    public override void Draw()
    {
        DrawControlBar();
        ImGui.Separator();
        DrawHuntList();
        ImGui.Spacing();
        DrawAddToggle();
        if (_showAddPanel) DrawAddPanel();
    }

    private void DrawControlBar()
    {
        var svc = Plugin.HuntService;

        if (svc.IsActive)
        {
            if (ImGui.Button("■ Arrêter", new Vector2(110, 0)))
                svc.Stop();
        }
        else
        {
            var hasTargets = Plugin.Config.HuntTargets.Any(t => t.Enabled);
            if (!hasTargets) ImGui.BeginDisabled();
            if (ImGui.Button("▶ Démarrer", new Vector2(110, 0)))
                svc.Start();
            if (!hasTargets) ImGui.EndDisabled();
        }

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - 30);
        if (ImGui.Button("⚙##cfg")) Plugin.ConfigWindow.Toggle();

        ImGui.TextColored(StateColor(svc.State), svc.StatusMessage);
    }

    private void DrawHuntList()
    {
        var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("huntlist", 5, flags)) return;

        ImGui.TableSetupColumn("",          ImGuiTableColumnFlags.WidthFixed,   20);
        ImGui.TableSetupColumn("Item",      ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Objectif",  ImGuiTableColumnFlags.WidthFixed,   65);
        ImGui.TableSetupColumn("Inventaire",ImGuiTableColumnFlags.WidthFixed,   80);
        ImGui.TableSetupColumn("",          ImGuiTableColumnFlags.WidthFixed,   26);
        ImGui.TableHeadersRow();

        var toRemove = -1;
        for (var i = 0; i < Plugin.Config.HuntTargets.Count; i++)
        {
            var t = Plugin.Config.HuntTargets[i];
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            var en = t.Enabled;
            if (ImGui.Checkbox($"##en{i}", ref en)) { t.Enabled = en; Plugin.Config.Save(); }

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(t.ItemName);

            ImGui.TableSetColumnIndex(2);
            var qty = t.TargetQuantity;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt($"##qty{i}", ref qty, 0) && qty > 0) { t.TargetQuantity = qty; Plugin.Config.Save(); }

            ImGui.TableSetColumnIndex(3);
            var current = GetInventoryCount(t.ItemId);
            var done    = current >= t.TargetQuantity;
            ImGui.TextColored(done ? new Vector4(0.4f, 1, 0.4f, 1) : new Vector4(1, 0.75f, 0.3f, 1),
                              $"{current} / {t.TargetQuantity}");

            ImGui.TableSetColumnIndex(4);
            if (ImGui.SmallButton($"X##rm{i}")) toRemove = i;
        }

        ImGui.EndTable();

        if (toRemove >= 0)
        {
            Plugin.Config.HuntTargets.RemoveAt(toRemove);
            Plugin.Config.Save();
        }
    }

    private void DrawAddToggle()
    {
        if (ImGui.Button(_showAddPanel ? "▲ Masquer" : "+ Ajouter un item"))
        {
            _showAddPanel = !_showAddPanel;
            if (_showAddPanel) { _searchQuery = ""; _searchResults = []; _selectedResult = -1; }
        }
    }

    private void DrawAddPanel()
    {
        ImGui.Separator();
        ImGui.TextUnformatted("Rechercher un item (drops de mobs uniquement) :");
        ImGui.SetNextItemWidth(320);
        if (ImGui.InputText("##search", ref _searchQuery, 64))
            RefreshSearch();

        if (_searchResults.Count == 0)
        {
            if (_searchQuery.Length >= 2)
                ImGui.TextDisabled("Aucun résultat.");
            return;
        }

        ImGui.SetNextItemWidth(320);
        if (ImGui.BeginListBox("##results", new Vector2(320, 120)))
        {
            for (var i = 0; i < _searchResults.Count; i++)
            {
                if (ImGui.Selectable(_searchResults[i].Name, _selectedResult == i))
                    _selectedResult = i;
            }
            ImGui.EndListBox();
        }

        if (_selectedResult < 0) return;

        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("##addqty", ref _addQuantity, 1, 10);
        if (_addQuantity < 1) _addQuantity = 1;
        ImGui.TextDisabled("quantité");

        if (ImGui.Button("Ajouter##confirm"))
        {
            var item = _searchResults[_selectedResult];
            if (!Plugin.Config.HuntTargets.Any(t => t.ItemId == item.Id))
            {
                Plugin.Config.HuntTargets.Add(new HuntTarget
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    TargetQuantity = _addQuantity
                });
                Plugin.Config.Save();
            }
            _showAddPanel = false;
        }
        ImGui.EndGroup();
    }

    private void RefreshSearch()
    {
        if (_searchQuery.Length < 2) { _searchResults = []; return; }

        var sheet = Plugin.DataManager.GetExcelSheet<Item>();
        if (sheet == null) return;

        _searchResults = sheet
            .Where(i => i.Name.ToString().Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
                     && Plugin.DropResolver.HasSources(i.RowId))
            .OrderBy(i => i.Name.ToString())
            .Take(20)
            .Select(i => (i.RowId, i.Name.ToString()))
            .ToList();

        _selectedResult = -1;
    }

    private static unsafe int GetInventoryCount(uint itemId)
    {
        var mgr = InventoryManager.Instance();
        return mgr != null ? (int)mgr->GetInventoryItemCount(itemId) : 0;
    }

    private static Vector4 StateColor(HuntState state) => state switch
    {
        HuntState.AllDone => new Vector4(0.4f, 1f,   0.4f, 1f),
        HuntState.Error   => new Vector4(1f,   0.3f, 0.3f, 1f),
        HuntState.Idle    => new Vector4(0.6f, 0.6f, 0.6f, 1f),
        _                 => new Vector4(1f,   0.85f,0.4f, 1f)
    };
}
