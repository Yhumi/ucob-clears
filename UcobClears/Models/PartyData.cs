using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UcobClears.Models
{
    class PartyData
    {
        public string username { get; set; }
        public string world { get; set; }
        public List<PartyDataMember> party { get; set; }
    }

    class PartyDataMember
    {
        public string username { get; set; }
        public string world { get; set; }
        public int? count { get; set; }
    }
}
