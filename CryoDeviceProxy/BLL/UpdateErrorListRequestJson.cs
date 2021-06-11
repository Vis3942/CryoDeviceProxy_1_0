using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>UpdateErrorListRequestJson</c> encapsulates the error list data that needs to be sent to the server
    /// </summary>
    public class UpdateErrorListRequestJson
    {
        public string device { get; set; }
        public DeviceError[] error_list { get; set; }
    }
}
