using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    public class PingResponseJson
    {
        public Data data { get; set; }
        public Meta meta { get; set; }
    }

    public class Data
    {
        public string serial_number { get; set; }
        public string imei_number { get; set; }
        public string service_tag { get; set; }
        public string response { get; set; }
    }

    public class Meta
    {
        public int code { get; set; }
        public string message { get; set; }
    }

}
