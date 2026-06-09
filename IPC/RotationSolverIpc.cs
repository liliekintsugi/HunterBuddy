using Dalamud.Plugin.Ipc;

namespace HunterBuddy.IPC;

public class RotationSolverIpc : IDisposable
{
    // RotationSolverReborn v4+ — StateCommandType: 0=Off, 1=Auto, 2=Manual
    private ICallGateSubscriber<byte, object>? _setState;

    public RotationSolverIpc()
    {
        try
        {
            _setState = Plugin.PluginInterface.GetIpcSubscriber<byte, object>("RotationSolver.SetState");
        }
        catch
        {
            Plugin.Log.Warning("[HunterBuddy] RotationSolverReborn IPC non disponible.");
        }
    }

    public void Enable()
    {
        try { _setState?.InvokeFunc(1); } // Auto
        catch (Exception e) { Plugin.Log.Warning(e, "RotationSolver.Enable échoué."); }
    }

    public void Disable()
    {
        try { _setState?.InvokeFunc(0); } // Off
        catch (Exception e) { Plugin.Log.Warning(e, "RotationSolver.Disable échoué."); }
    }

    public void Dispose() { }
}
