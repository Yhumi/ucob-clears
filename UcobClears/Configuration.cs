using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace UcobClears;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public string FFLogsAPI_ClientId { get; set; } = string.Empty;
    public string FFLogsAPI_ClientSecret { get; set; } = string.Empty;
    public bool ShowErrorsOnPlate { get; set; } = false;
    public int CacheValidityInMinutes { get; set; } = 15;

    // the below exist just to make saving less cumbersome
    public void Save(bool ignoreCacheOnRefresh = false, bool skipReload = false)
    {
        Svc.PluginInterface.SavePluginConfig(this);
        
        if (!skipReload)
            P.AdvPlateUI.Refresh(ignoreCacheOnRefresh);
    }

    public static Configuration Load()
    {
        try
        {
            var contents = File.ReadAllText(Svc.PluginInterface.ConfigFile.FullName);
            var json = JObject.Parse(contents);
            var version = (int?)json["Version"] ?? 0;
            return json.ToObject<Configuration>() ?? new();
        }
        catch (Exception e)
        {
            Svc.Log.Error($"Failed to load config from {Svc.PluginInterface.ConfigFile.FullName}: {e}");
            return new();
        }
    }
}