using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UcobClears.Models
{
    internal class FFLogsStatus
    {
        public FFLogsStatus() { }

        public FFLogsRequestStatus requestStatus {  get; set; }
        public string message { get; set; }
        public bool? checkProg { get; set; }
        public int? kills { get; set; } = 0;
    }

    public enum FFLogsRequestStatus
    {
        Success,
        Failed,
        Searching
    }
}
