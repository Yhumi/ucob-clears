using ECommons.DalamudServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UcobClears.RawInformation
{
    internal static class ConstantData
    {
        public static Dictionary<string, string>? ServerRegionMap;

        public static void Init()
        {
            try
            {
                var filePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "RawInformation/Data/Region.json");
                var jsonData = File.ReadAllText(filePath);

                ServerRegionMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Failed to load config from {Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "RawInformation/Data/Region.json")}: {e}");
            }
        }
    }
}
