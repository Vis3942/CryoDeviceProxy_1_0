using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    public class UpdateErrorListRequestJson
    {
        public string device { get; set; }
        public DeviceError[] error_list { get; set; }
    }
}
