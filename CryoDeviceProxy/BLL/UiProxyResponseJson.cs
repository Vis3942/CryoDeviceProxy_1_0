using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    public class UiProxyResponseJson
    {
        public UIData data { get; set; }
        public UIMeta meta { get; set; }
    }
    public class UIData
    {
        public string updated_at { get; set; }
        public string ping_data { get; set; }
        public string response { get; set; }

    }

    public class UIMeta
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}
