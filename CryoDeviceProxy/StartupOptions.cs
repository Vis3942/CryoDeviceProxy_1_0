using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy
{
    public class StartupOptions
    {
        public string LogTo { get; set; }
        public string LogLevel { get; set; }
        public float MaxLogFileSizeMB { get; set; }
        public int MaxCountFiles { get; set; }
        public string LogFileName { get; set; }
    }
}
