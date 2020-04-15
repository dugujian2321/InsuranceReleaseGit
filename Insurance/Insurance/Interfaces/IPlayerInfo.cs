using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualCredit.PlayerInfo
{
    public interface IPlayerInfo
    {
        string ID { get; set; }
        string ServerName { get; set; }
        string Career { get; set; }
    }
}
