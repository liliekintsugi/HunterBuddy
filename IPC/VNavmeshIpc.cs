using Dalamud.Plugin.Ipc;

namespace HunterBuddy.IPC;

public class VNavmeshIpc : IDisposable
{
    private readonly ICallGateSubscriber<float, float, float, bool, Task<bool>> _moveTo;
    private readonly ICallGateSubscriber<bool> _isReady;
    private readonly ICallGateSubscriber<bool> _isRunning;
    private readonly ICallGateSubscriber<object> _stop;

    public VNavmeshIpc()
    {
        _moveTo    = Plugin.PluginInterface.GetIpcSubscriber<float, float, float, bool, Task<bool>>("vnavmesh.SimpleMove.PathfindAndMoveTo");
        _isReady   = Plugin.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
        _isRunning = Plugin.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.IsRunning");
        _stop      = Plugin.PluginInterface.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
    }

    public bool IsReady()
    {
        try { return _isReady.InvokeFunc(); }
        catch { return false; }
    }

    public bool IsMoving()
    {
        try { return _isRunning.InvokeFunc(); }
        catch { return false; }
    }

    public void MoveTo(float x, float y, float z, bool fly = false)
    {
        try { _moveTo.InvokeFunc(x, y, z, fly); }
        catch (Exception e) { Plugin.Log.Warning(e, "vnavmesh.MoveTo échoué."); }
    }

    public void Stop()
    {
        try { _stop.InvokeFunc(); }
        catch { }
    }

    public void Dispose() { }
}
