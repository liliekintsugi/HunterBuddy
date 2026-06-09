using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HunterBuddy.Services;
using HunterBuddy.Windows;

namespace HunterBuddy;

public sealed class Plugin : IDalamudPlugin
{
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    public static ICommandManager         CommandManager  { get; private set; } = null!;
    public static IClientState            ClientState     { get; private set; } = null!;
    public static IObjectTable            ObjectTable     { get; private set; } = null!;
    public static ITargetManager          TargetManager   { get; private set; } = null!;
    public static IFramework              Framework       { get; private set; } = null!;
    public static IChatGui                ChatGui         { get; private set; } = null!;
    public static IPluginLog              Log             { get; private set; } = null!;
    public static IDataManager            DataManager     { get; private set; } = null!;

    public static Configuration Config       { get; private set; } = null!;
    public static DropResolver  DropResolver { get; private set; } = null!;
    public static HuntService   HuntService  { get; private set; } = null!;
    public static ConfigWindow  ConfigWindow { get; private set; } = null!;

    private readonly WindowSystem _windowSystem = new("HunterBuddy");
    private readonly MainWindow   _mainWindow;

    private const string CmdMain  = "/hunterbuddy";
    private const string CmdShort = "/hb";

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager         commandManager,
        IClientState            clientState,
        IObjectTable            objectTable,
        ITargetManager          targetManager,
        IFramework              framework,
        IChatGui                chatGui,
        IPluginLog              log,
        IDataManager            dataManager)
    {
        PluginInterface = pluginInterface;
        CommandManager  = commandManager;
        ClientState     = clientState;
        ObjectTable     = objectTable;
        TargetManager   = targetManager;
        Framework       = framework;
        ChatGui         = chatGui;
        Log             = log;
        DataManager     = dataManager;

        Config       = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        DropResolver = new DropResolver();
        HuntService  = new HuntService();

        _mainWindow  = new MainWindow();
        ConfigWindow = new ConfigWindow();
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(ConfigWindow);

        pluginInterface.UiBuilder.Draw          += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi    += _mainWindow.Toggle;
        pluginInterface.UiBuilder.OpenConfigUi  += ConfigWindow.Toggle;

        CommandManager.AddHandler(CmdMain,  new CommandInfo(OnCommand) { HelpMessage = "Ouvre HunterBuddy (/hb config pour les paramètres)" });
        CommandManager.AddHandler(CmdShort, new CommandInfo(OnCommand) { HelpMessage = "Ouvre HunterBuddy" });

        Framework.Update += HuntService.OnFrameworkUpdate;
    }

    private void OnCommand(string _, string args)
    {
        if (args.Trim() == "config")
            ConfigWindow.Toggle();
        else
            _mainWindow.Toggle();
    }

    public void Dispose()
    {
        Framework.Update -= HuntService.OnFrameworkUpdate;
        CommandManager.RemoveHandler(CmdMain);
        CommandManager.RemoveHandler(CmdShort);
        PluginInterface.UiBuilder.Draw         -= _windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   -= _mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
        _windowSystem.RemoveAllWindows();
        HuntService.Dispose();
    }
}
