using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UcobClears.RawInformation
{
    internal class LuminaData
    {
        public static Dictionary<uint, World>? WorldSheet;

        public static void Init()
        {
            WorldSheet = Svc.Data?.GetExcelSheet<World>()?
                        .ToDictionary(i => i.RowId, i => i);
        }

        public static World? GetWorld(ushort world)
        {
            return WorldSheet?.FirstOrNull(x => x.Key == world)?.Value ?? null;
        }
    }
}
