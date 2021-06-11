using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>UiProxyRequestJson</c> encapsulates request to be sent to the server. Not used in this version.
    /// </summary>
    public class UiProxyRequestJson
    {
        public string service_tag { get; set; }
        public string email { get; set; }
    }
}
