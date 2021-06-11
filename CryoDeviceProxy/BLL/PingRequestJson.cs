using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>PingRequestJson</c> encapsulates ping data sent to the device if the serial number and imei number are available
    /// </summary>
    public class PingRequestJson
    {
        public string serial_number { get; set; }
        public string imei_number { get; set; }
        public string ping_data{ get; set; }

    }
}
