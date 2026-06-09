using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using HunterBuddy.IPC;
using HunterBuddy.Models;

namespace HunterBuddy.Services;

public enum HuntState
{
    Idle,
    SelectingTarget,
    NavigatingToSpawn,
    SearchingMob,
    Engaging,
    InCombat,
    Looting,
    CheckingInventory,
    AllDone,
    Error
}

public class HuntService : IDisposable
{
    public HuntState State { get; private set; } = HuntState.Idle;
    public string StatusMessage { get; private set; } = "En attente.";
    public bool IsActive => State is not (HuntState.Idle or HuntState.AllDone or HuntState.Error);

    private HuntTarget? _currentTarget;
    private DropSource? _currentSource;
    private SpawnPosition? _currentSpawn;
    private DateTime _lastStateChange = DateTime.MinValue;

    private readonly VNavmeshIpc _vnavmesh;
    private readonly RotationSolverIpc _rotationSolver;

    public HuntService()
    {
        _vnavmesh = new VNavmeshIpc();
        _rotationSolver = new RotationSolverIpc();
    }

    public void Start()
    {
        if (IsActive) return;
        SetState(HuntState.SelectingTarget, "Sélection de la cible...");
    }

    public void Stop()
    {
        _vnavmesh.Stop();
        _rotationSolver.Disable();
        _currentTarget = null;
        _currentSource = null;
        _currentSpawn  = null;
        SetState(HuntState.Idle, "Arrêté.");
    }

    public void OnFrameworkUpdate(IFramework _)
    {
        if (!IsActive) return;
        if (Plugin.ObjectTable.LocalPlayer == null) { Stop(); return; }

        try
        {
            switch (State)
            {
                case HuntState.SelectingTarget:    TickSelectTarget();    break;
                case HuntState.NavigatingToSpawn:  TickNavigating();      break;
                case HuntState.SearchingMob:       TickSearchMob();       break;
                case HuntState.Engaging:           TickEngaging();        break;
                case HuntState.InCombat:           TickCombat();          break;
                case HuntState.Looting:            TickLooting();         break;
                case HuntState.CheckingInventory:  TickCheckInventory();  break;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "[HunterBuddy] Erreur dans OnFrameworkUpdate.");
            SetState(HuntState.Error, $"Erreur : {e.Message}");
        }
    }

    // ─── States ───────────────────────────────────────────────────────────────

    private void TickSelectTarget()
    {
        var target = Plugin.Config.HuntTargets
            .FirstOrDefault(t => t.Enabled && GetInventoryCount(t.ItemId) < t.TargetQuantity);

        if (target == null)
        {
            SetState(HuntState.AllDone, "Tous les objectifs sont atteints !");
            return;
        }

        var sources = Plugin.DropResolver.GetSources(target.ItemId);
        if (sources.Count == 0)
        {
            SetState(HuntState.Error, $"Aucune source de drop connue pour : {target.ItemName}");
            return;
        }

        _currentTarget = target;
        // Priorité aux sources avec le meilleur taux de drop
        _currentSource = sources.OrderByDescending(s => s.DropRate).First();
        _currentSpawn  = _currentSource.Positions.FirstOrDefault();

        // Pas de position connue : on scanne directement la zone sans navigation fixe
        if (_currentSpawn == null)
        {
            SetState(HuntState.SearchingMob,
                $"Scan zone pour {_currentSource.MobName} ({_currentSource.ZoneName}) — naviguez vers la zone si besoin.");
            return;
        }

        SetState(HuntState.NavigatingToSpawn, $"Navigation vers {_currentSource.MobName} ({_currentSource.ZoneName})...");
    }

    private void TickNavigating()
    {
        if (_currentSource == null || _currentSpawn == null)
        {
            SetState(HuntState.Error, "Données de navigation invalides.");
            return;
        }

        if (!_vnavmesh.IsReady())
        {
            StatusMessage = "Attente de vnavmesh...";
            return;
        }

        var player = Plugin.ObjectTable.LocalPlayer!;
        var dist = Dist2D(player.Position.X, player.Position.Z, _currentSpawn.X, _currentSpawn.Z);

        if (dist <= Plugin.Config.NavigationTolerance)
        {
            _vnavmesh.Stop();
            SetState(HuntState.SearchingMob, $"Recherche de {_currentSource.MobName}...");
            return;
        }

        if (!_vnavmesh.IsMoving())
            _vnavmesh.MoveTo(_currentSpawn.X, _currentSpawn.Y, _currentSpawn.Z);
    }

    private void TickSearchMob()
    {
        if (_currentSource == null) return;

        var mob = FindMob(_currentSource.BNpcNameId);
        if (mob != null)
        {
            Plugin.TargetManager.Target = mob;
            SetState(HuntState.Engaging, $"Mob trouvé : {mob.Name}");
            return;
        }

        if (TimeSinceState() <= TimeSpan.FromSeconds(Plugin.Config.MobSearchTimeoutSec)) return;

        if (_currentSource.Positions.Count == 0)
        {
            // Aucune position connue : on attend que le joueur soit dans la bonne zone
            StatusMessage = $"{_currentSource.MobName} introuvable — êtes-vous dans : {_currentSource.ZoneName} ?";
            _lastStateChange = DateTime.UtcNow; // reset timeout pour ne pas spammer
            return;
        }

        // Tenter un autre point de spawn si disponible
        if (_currentSource.Positions.Count > 1)
        {
            var idx = (_currentSource.Positions.IndexOf(_currentSpawn!) + 1) % _currentSource.Positions.Count;
            _currentSpawn = _currentSource.Positions[idx];
            SetState(HuntState.NavigatingToSpawn, $"Repositionnement vers spawn {idx + 1}/{_currentSource.Positions.Count}...");
        }
        else
        {
            SetState(HuntState.NavigatingToSpawn, "Mob introuvable, retour au point de spawn...");
        }
    }

    private void TickEngaging()
    {
        var mob = Plugin.TargetManager.Target as IBattleChara;
        if (mob == null || mob.IsDead || mob.CurrentHp == 0)
        {
            SetState(HuntState.Looting, "Mob mort, pillage...");
            return;
        }

        var player = Plugin.ObjectTable.LocalPlayer!;
        if (player.StatusFlags.HasFlag(StatusFlags.InCombat))
        {
            if (Plugin.Config.UseRotationSolver)
                _rotationSolver.Enable();
            SetState(HuntState.InCombat, $"En combat : {mob.Name}");
            return;
        }

        if (TimeSinceState() < TimeSpan.FromMilliseconds(500)) return;

        var dist = Dist2D(player.Position.X, player.Position.Z, mob.Position.X, mob.Position.Z);
        if (dist > 3f)
            _vnavmesh.MoveTo(mob.Position.X, mob.Position.Y, mob.Position.Z);
        else
        {
            _vnavmesh.Stop();
            UseAutoAttack();
        }
    }

    private void TickCombat()
    {
        var mob = Plugin.TargetManager.Target as IBattleChara;
        if (mob == null || mob.IsDead || mob.CurrentHp == 0)
        {
            _rotationSolver.Disable();
            SetState(HuntState.Looting, "Combat terminé, pillage...");
            return;
        }

        StatusMessage = $"Combat : {mob.Name} ({mob.CurrentHp} / {mob.MaxHp} HP)";
    }

    private void TickLooting()
    {
        if (TimeSinceState() > TimeSpan.FromMilliseconds(Plugin.Config.LootWaitMs))
            SetState(HuntState.CheckingInventory, "Vérification de l'inventaire...");
    }

    private void TickCheckInventory()
    {
        if (_currentTarget == null) { Stop(); return; }

        var current = GetInventoryCount(_currentTarget.ItemId);
        var needed  = _currentTarget.TargetQuantity;

        if (current >= needed)
            SetState(HuntState.SelectingTarget, $"{_currentTarget.ItemName} : objectif atteint ({current}/{needed}) !");
        else
            SetState(HuntState.NavigatingToSpawn, $"{_currentTarget.ItemName} : {current}/{needed} — on continue...");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private IBattleChara? FindMob(uint bNpcNameId)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is IBattleChara bc && bc.NameId == bNpcNameId && !bc.IsDead && bc.CurrentHp > 0)
                return bc;
        }
        return null;
    }

    private unsafe void UseAutoAttack()
    {
        ActionManager.Instance()->UseAction(ActionType.Action, 7);
    }

    private unsafe int GetInventoryCount(uint itemId)
    {
        var mgr = InventoryManager.Instance();
        return mgr != null ? (int)mgr->GetInventoryItemCount(itemId) : 0;
    }

    private static float Dist2D(float x1, float z1, float x2, float z2)
        => MathF.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));

    private void SetState(HuntState state, string msg)
    {
        State = state;
        StatusMessage = msg;
        _lastStateChange = DateTime.UtcNow;
        Plugin.Log.Debug($"[HunterBuddy] → {state}: {msg}");
    }

    private TimeSpan TimeSinceState() => DateTime.UtcNow - _lastStateChange;

    public void Dispose()
    {
        _vnavmesh.Dispose();
        _rotationSolver.Dispose();
    }
}
