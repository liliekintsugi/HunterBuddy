using System.Numerics;
using Dalamud.Plugin.Ipc;

namespace HunterBuddy.IPC;

public class VNavmeshIpc : IDisposable
{
    // Signatures vérifiées sur https://github.com/awgil/ffxiv_navmesh
    private readonly ICallGateSubscriber<Vector3, bool, bool> _moveTo;
    private readonly ICallGateSubscriber<bool>                _isReady;
    private readonly ICallGateSubscriber<bool>                _pathfindInProgress;
    private readonly ICallGateSubscriber<object>              _stop;

    public VNavmeshIpc()
    {
        _moveTo             = Plugin.PluginInterface.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        _isReady            = Plugin.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _pathfindInProgress = Plugin.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");
        _stop               = Plugin.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    }

    public bool IsReady()
    {
        try { return _isReady.InvokeFunc(); }
        catch { return false; }
    }

    public bool IsMoving()
    {
        try { return _pathfindInProgress.InvokeFunc(); }
        catch { return false; }
    }

    public void MoveTo(float x, float y, float z, bool fly = false)
    {
        try { _moveTo.InvokeFunc(new Vector3(x, y, z), fly); }
        catch (Exception e) { Plugin.Log.Warning(e, "vnavmesh.MoveTo échoué."); }
    }

    public void Stop()
    {
        try { _stop.InvokeFunc(); }
        catch { }
    }

    public void Dispose() { }
}
