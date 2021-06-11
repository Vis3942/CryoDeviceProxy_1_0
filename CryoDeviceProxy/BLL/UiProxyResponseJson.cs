using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>UiProxyResponseJson</c> encapsulates the information received from server
    /// </summary>
    public class UiProxyResponseJson
    {
        public UIData data { get; set; }
        public UIMeta meta { get; set; }
    }

    /// <summary>
    /// The class <c>UIData</c> encapsulates the response sent from the server for the get-info request from the user interface
    /// </summary>
    public class UIData
    {
        public string updated_at { get; set; }
        public string ping_data { get; set; }
        public string response { get; set; }

    }

    /// <summary>
    /// The class <c>UIMeta</c> encapsulates the additional connection status information from the server
    /// </summary>
    public class UIMeta
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}
