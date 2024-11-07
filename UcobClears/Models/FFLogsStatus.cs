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
    }

    public enum FFLogsRequestStatus
    {
        Success,
        Failed,
        Searching
    }
}
