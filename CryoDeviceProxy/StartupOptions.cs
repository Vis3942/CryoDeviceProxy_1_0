using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy
{
    /// <summary>
    /// The class <c>StartupOptions</c> has all the fields corresponding to the parameters defined in the 
    /// startup_settings.json file.
    /// </summary>
    public class StartupOptions
    {
        /// <summary>
        /// This property will set the destination of logged data. Supported values are "console", "file".
        /// </summary>
        public string LogTo { get; set; }

        /// <summary>
        /// This property sets the log level which filters the messages that will be logged based on the current log level
        /// </summary>
        public string LogLevel { get; set; }

        /// <summary>
        /// This property specifies the maximum size in megabytes of a single log file
        /// </summary>
        public float MaxLogFileSizeMB { get; set; }

        /// <summary>
        /// This property specifies the maximum count of backup log files the logger should store
        /// </summary>
        public int MaxCountFiles { get; set; }

        /// <summary>
        /// This property sets the default log file name for the application
        /// </summary>
        public string LogFileName { get; set; }

        /// <summary>
        /// This property sets the timeout in seconds for SSL Authentication after device establishes connection
        /// </summary>
        public int DeviceSSLAuthTimeoutSecs { get; set; }

        /// <summary>
        /// This property sets the read timeout for reading from device stream after authentication
        /// </summary>
        public int DeviceReadStreamTimeoutSecs { get; set; }
    }
}
