using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>DeviceError</c> encapsulates the error data information received from device
    /// </summary>
    public class DeviceError
    {
        public string occured_at { get; set; }
        public string error_code { get; set; }
    }
}
