using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>UpdateUsageStatsListRequestJson</c> encapsulates the usage statistics list information to be sent to the server
    /// </summary>
    public class UpdateUsageStatsListRequestJson
    {
        public string device { get; set; }
        public DeviceUsageStats[] usagestats_list { get; set; }

    }
}
