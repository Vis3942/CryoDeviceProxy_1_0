using CryoDeviceProxy.BLL;
using System;
using System.Net;

namespace CryoDeviceProxy
{
    class Program
    {
        private static DeviceBLL devbll = null;
        public static DeviceBLL GetDeviceBLL()
        {
            if (devbll == null)
            {
                devbll = new DeviceBLL();
            }
            return devbll;
        }

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


            Console.WriteLine("Starting CryoConcepts Device Highway / Control.");
            Console.WriteLine(string.Format("[isproduction {0}], ErrorList: read -> {1}, clear -> {2}, UsageList: read-> {3}, clear -> {4}",
                isProduction, readErrors, clearErrors, readUsage, clearUsage));
            CryoServer cs = new CryoServer();
            cs.IsProduction = isProduction;
            cs.ReadError = readErrors;
            cs.ClearError = clearErrors;
            cs.ReadUsage = readUsage;
            cs.ClearUsageStats = clearUsage;
            //CryoServer cs = new CryoServer(IPAddress.Any.ToString());
            cs.StartAllServers();
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            //Console.ReadLine();
        }
    }
}
