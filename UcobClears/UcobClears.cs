using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using UcobClears.UI;
using ECommons;
using ECommons.DalamudServices;
using Dalamud.Interface.Style;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentPartyMember.Delegates;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.CharaView.Delegates;
using UcobClears.AdvPlate;
using UcobClears.RawInformation;
using KamiToolKit;
using UcobClears.PartyList;

namespace UcobClears;

public unsafe class UcobClears : IDalamudPlugin
{
    public string Name => "UcobClears";
    private const string CommandNameAlt = "/cobclears";
    private const string CommandName = "/ucobclears";
    private const int CurrentConfigVersion = 1;

    internal static UcobClears P = null!;
    internal PluginUI PluginUi;
    internal Configuration Config;
    internal WindowSystem ws;
    internal NativeController NativeController;

    //internal AdvPlateUI AdvPlateUI;
    internal AdvPlateController AdvPlateController;
    internal PartyListController PartyListController;

    internal StyleModel Style;
    internal bool StylePushed = false;

    public UcobClears(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.All);
        P = this;

        ConstantData.Init();
        P.Config = Configuration.Load();

        LuminaData.Init();

        NativeController = new NativeController(pluginInterface);

        //AdvPlateUI = new();
        AdvPlateController = new();
        ws = new();
        //ws.AddWindow(AdvPlateUI);
        Config = P.Config;
        PluginUi = new();

        PartyListController = new();

        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the UcobClears settings.\n",
            ShowInHelp = true,
        });

        Svc.Commands.AddHandler(CommandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the UcobClears settings.\n",
            ShowInHelp = true,
        });

        Svc.PluginInterface.UiBuilder.Draw += ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawSettingsUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi += DrawSettingsUI;

        Style = StyleModel.GetFromCurrent()!;
    }

    public void Dispose()
    {
        PluginUi.Dispose();

        GenericHelpers.Safe(() => Svc.Commands.RemoveHandler(CommandName));
        GenericHelpers.Safe(() => Svc.Commands.RemoveHandler(CommandNameAlt));

        Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawSettingsUI;
        Svc.PluginInterface.UiBuilder.Draw -= ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= DrawSettingsUI;

        GenericHelpers.Safe(NativeController.Dispose);

        AdvPlateController.Dispose();

        ws?.RemoveAllWindows();
        ws = null!;

        ECommonsMain.Dispose();
        P = null!;
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        PluginUi.IsOpen = true;
    }

    private void DrawSettingsUI()
    {
        PluginUi.IsOpen = true;
    }
}
