using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    public class PingRequestJson
    {
        public string serial_number { get; set; }
        public string imei_number { get; set; }
        public string ping_data{ get; set; }

    }
}
