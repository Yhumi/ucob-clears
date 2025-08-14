using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;

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

        bool uploadPartyClearsData = P.Config.UploadPartyClearsData;
        string uploadPartyDataUrl = P.Config.UploadPartyDataUrl;
        string uploadPartyDataApiKey = P.Config.UploadPartyDataAPIKey;
        int uploadPartyCacheValidityInMinutes = P.Config.UploadPartyCacheValidityInMinutes;
        bool useWebSocket = P.Config.UseWebSocket;
        string webSocketPort = P.Config.WebSocketPort;

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
        if (ImGui.SliderInt("###PlateCache", ref CacheValidityInMinutes, 5, 120))
        {
            P.Config.CacheValidityInMinutes = CacheValidityInMinutes;
            P.Config.Save(true);
        }

        if (ImGui.Checkbox("Upload Party Data", ref uploadPartyClearsData))
        {
            P.Config.UploadPartyClearsData = uploadPartyClearsData;
            P.Config.Save(true);

            if (uploadPartyClearsData)
            {
                P.PartyListController.AddEventHandling();
                //P.PartyListController.ForcePartyListUpload();
            }   
            else
            {
                P.PartyListController.RemoveEventHandling();
            }
                
        }

        if (uploadPartyClearsData)
        {
            if (ImGui.Checkbox("Use WebSocket", ref useWebSocket))
            {
                P.Config.UseWebSocket = useWebSocket;
                P.Config.Save(true);
            }

            if (useWebSocket)
            {
                ImGui.Text("WebSocket Port");
                if (ImGui.InputText("###WebSocketPort", ref webSocketPort, 6))
                {
                    P.Config.WebSocketPort = webSocketPort;
                    P.Config.Save(true);
                }
            }

            ImGui.Text("Party Data URL");
            if (ImGui.InputText("###PartyDataUrl", ref uploadPartyDataUrl, 150))
            {
                P.Config.UploadPartyDataUrl = uploadPartyDataUrl;
                P.Config.Save(true);
            }

            ImGui.Text("Party Data Bearer");
            if (ImGui.InputText("###PartyDataApiKey", ref uploadPartyDataApiKey, 150))
            {
                P.Config.UploadPartyDataAPIKey = uploadPartyDataApiKey;
                P.Config.Save(true);
            }

            ImGui.Text("Party Data Cache Validity");
            ImGuiEx.InfoMarker("In case you want the cache validty to last longer while waiting.");
            if (ImGui.SliderInt("###PartyCache", ref uploadPartyCacheValidityInMinutes, 5, 120))
            {
                P.Config.UploadPartyCacheValidityInMinutes = uploadPartyCacheValidityInMinutes;
                P.Config.Save(true);
            }
        }
    }
}
