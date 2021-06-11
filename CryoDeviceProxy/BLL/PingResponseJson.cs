using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>PingResponseJson</c> encapsulates the response from the server
    /// </summary>
    public class PingResponseJson
    {
        public Data data { get; set; }
        public Meta meta { get; set; }
    }

    /// <summary>
    /// The class <c>Data</c> encapsulates the response to a ping packet from the server
    /// </summary>
    public class Data
    {
        public string serial_number { get; set; }
        public string imei_number { get; set; }
        public string service_tag { get; set; }
        public string response { get; set; }
    }

    /// <summary>
    /// The class <c>Meta</c> encapsulates the connection status information and additional messages sent from the server
    /// </summary>
    public class Meta
    {
        public int code { get; set; }
        public string message { get; set; }
    }

}
