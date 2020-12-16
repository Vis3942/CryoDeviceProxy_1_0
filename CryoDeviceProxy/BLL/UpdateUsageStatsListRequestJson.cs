using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    public class UpdateUsageStatsListRequestJson
    {
        public string device { get; set; }
        public DeviceUsageStats[] usagestats_list { get; set; }

    }
}
