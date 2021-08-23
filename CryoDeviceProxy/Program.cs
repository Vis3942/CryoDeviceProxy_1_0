using CryoDeviceProxy.BLL;
using CryoDeviceProxy.Logs;
using System;
using System.Net;

namespace CryoDeviceProxy
{
    class Program
    {
        //private static DeviceBLL devbll = null;

        ///// <summary>
        ///// This static function provides the singleton interface that allows communication to the backend server
        ///// </summary>
        ///// <returns></returns>
        //public static DeviceBLL GetDeviceBLL()
        //{
        //    if (devbll == null)
        //    {
        //        devbll = new DeviceBLL();
        //    }
        //    return devbll;
        //}


        /// <summary>
        /// This is entry point of the application.
        /// For debugging, (that is not in production), application can be executed
        /// to control specific features, which could be true/false. True allows execution, false prevents execution of feature. They are:
        /// <list type="bullet">
        /// <item><description>Read Errors from device if available</description></item>
        /// <item><description>Clear errors on successful read</description></item>
        /// <item><description>Read Usage Statistics from device if available</description></item>
        /// <item><description>Clear Usage statistics from device if successfully read from device</description></item>
        /// </list>
        /// This main function also setups the logging framework, and starts the application.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            bool isProduction = true;
            bool readErrors = true;
            bool clearErrors = true;
            bool readUsage = true;
            bool clearUsage = true;

            for (int ii=0; ii < args.Length; ii++)
            {
                if (args[ii].ToLower().Equals("production"))
                {
                    isProduction = true;
                    readErrors = true;
                    clearErrors = true;
                    readUsage = true;
                    clearUsage = true;
                }
                if ( (args[ii].ToLower().Equals("debug")) || (args[ii].ToLower().Equals("debug")) )
                {
                    isProduction = false;
                    readErrors = false;
                    clearErrors = false;
                    readUsage = false;
                    clearUsage = false;
                }
                else if ( (args[ii].ToLower().Equals("readerror")) || (args[ii].ToLower().Equals("readerrors")) )
                {
                    readErrors = true;
                }
                else if ((args[ii].ToLower().Equals("clearerror")) || (args[ii].ToLower().Equals("clearerrors")))
                {
                    if (readErrors)
                    {
                        clearErrors = true;
                    }
                    else
                    {
                        clearErrors = false;
                    }
                }
                else if (args[ii].ToLower().Equals("readusage")) 
                {
                    readUsage = true;
                }
                else if (args[ii].ToLower().Equals("clearusage"))
                {
                    if (readUsage)
                    {
                        clearUsage = true;
                    }
                    else
                    {
                        clearUsage = false;
                    }
                }

            }



            try
            {
                AppOptions.LoadOptions();
                CDPLogger.InitLogger(
                    log_to: AppOptions.GetAppOptions.LogTo,
                    log_level: AppOptions.GetAppOptions.LogLevel,
                    max_file_count: AppOptions.GetAppOptions.MaxCountFiles,
                    max_file_size_mb: AppOptions.GetAppOptions.MaxLogFileSizeMB,
                    log_file_name: AppOptions.GetAppOptions.LogFileName);

                Console.WriteLine("Starting CryoConcepts Device Highway / Control.");
                Console.WriteLine(string.Format("[isproduction {0}], ErrorList: read -> {1}, clear -> {2}, UsageList: read-> {3}, clear -> {4}",
                    isProduction, readErrors, clearErrors, readUsage, clearUsage));
                CryoServer cs = new CryoServer
                {
                    IsProduction = isProduction,
                    ReadError = readErrors,
                    ClearError = clearErrors,
                    ReadUsage = readUsage,
                    ClearUsageStats = clearUsage
                };
                //CryoServer cs = new CryoServer(IPAddress.Any.ToString());
                cs.StartAllServers();
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                //Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Cryo Socket Server Exited.");
            }
        }
    }
}
