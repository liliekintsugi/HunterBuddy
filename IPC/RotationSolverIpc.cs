using Dalamud.Plugin.Ipc;

namespace HunterBuddy.IPC;

public class RotationSolverIpc : IDisposable
{
    // Vérifié sur https://github.com/FFXIV-CombatReborn/RotationSolverReborn
    // StateCommandType : 0=Off, 1=Auto, 2=TargetOnly, 3=Manual
    private ICallGateSubscriber<byte, object>? _changeMode;

    public RotationSolverIpc()
    {
        try
        {
            _changeMode = Plugin.PluginInterface.GetIpcSubscriber<byte, object>("RotationSolverReborn.ChangeOperatingMode");
        }
        catch
        {
            Plugin.Log.Warning("[HunterBuddy] RotationSolverReborn IPC non disponible.");
        }
    }

    public void Enable()
    {
        try { _changeMode?.InvokeFunc(1); } // Auto
        catch (Exception e) { Plugin.Log.Warning(e, "RSR.ChangeOperatingMode(Auto) échoué."); }
    }

    public void Disable()
    {
        try { _changeMode?.InvokeFunc(0); } // Off
        catch (Exception e) { Plugin.Log.Warning(e, "RSR.ChangeOperatingMode(Off) échoué."); }
    }

    public void Dispose() { }
}
