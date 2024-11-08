using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using ImGuiNET;

namespace UcobClears.UI;

public class PluginUI : Window, IDisposable
{
    private bool visible = false;

    public bool Visible
    {
        get { return this.visible; }
        set { this.visible = value; }
    }

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###UcobClears", ImGuiWindowFlags.NoResize)
    {
        this.RespectCloseHotkey = false;
        //this.SizeConstraints = new()
        //{
        //    MinimumSize = new(100, 100),
        //};
        P.ws.AddWindow(this);
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        
    }

    public override void Draw()
    {
        ImGui.TextWrapped($"Use an FFLogs API v2 Client Id and Client Secret.");
        if (ImGui.Button("FFlogs Client Setup Page"))
        {
            Util.OpenLink("https://www.fflogs.com/api/clients/");
        }

        string FFLogsAPI_ClientId = P.Config.FFLogsAPI_ClientId;
        string FFLogsAPI_ClientSecret = P.Config.FFLogsAPI_ClientSecret;
        string Tomestone_APIKey = P.Config.Tomestone_APIKey;
        bool ShowErrorsOnPlate = P.Config.ShowErrorsOnPlate;
        int CacheValidityInMinutes = P.Config.CacheValidityInMinutes;

        ImGui.Separator();

        ImGui.Text("FFLogs ClientId");
        if (ImGui.InputText("###FFLogsAPIKey", ref FFLogsAPI_ClientId, 150))
        {
            P.Config.FFLogsAPI_ClientId = FFLogsAPI_ClientId;
            P.Config.Save(true);
        }

        ImGui.Text("FFLogs ClientSecret");
        if (ImGui.InputText("###FFLogsAPISecret", ref FFLogsAPI_ClientSecret, 150))
        {
            P.Config.FFLogsAPI_ClientSecret = FFLogsAPI_ClientSecret;
            P.Config.Save(true);
        }

        ImGui.Text("Tomestone API Key");
        if (ImGui.InputText("###TomestoneAPI", ref Tomestone_APIKey, 150))
        {
            P.Config.Tomestone_APIKey = Tomestone_APIKey;
            P.Config.Save(true);
        }

        if (ImGui.Checkbox("Show Error Messages on Adventure Plate", ref ShowErrorsOnPlate))
        {
            P.Config.ShowErrorsOnPlate = ShowErrorsOnPlate;
            P.Config.Save(true);
        }

        ImGui.Text("Cache Validity in Minutes");
        if (ImGui.SliderInt("", ref CacheValidityInMinutes, 5, 120))
        {
            P.Config.CacheValidityInMinutes = CacheValidityInMinutes;
            P.Config.Save(true);
        }
    }
}
