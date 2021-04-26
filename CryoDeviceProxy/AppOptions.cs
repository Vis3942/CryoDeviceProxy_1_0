using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy
{
    public class AppOptions
    {
        #region var
        private static readonly string OptionsFileName = "startup_settings.json";
        private static bool OptionsLoaded = false;
        private static StartupOptions startOptions = null;
        #endregion //var

        public static StartupOptions GetAppOptions
        {
            get
            {
                if (startOptions == null)
                {
                    LoadOptions();
                }
                return startOptions;
            }
        }

        public static void LoadOptions()
        {
            if (!OptionsLoaded)
            {
                if (System.IO.File.Exists(OptionsFileName))
                {

                    try
                    {
                        string jsonstr = System.IO.File.ReadAllText(OptionsFileName);
                        startOptions = System.Text.Json.JsonSerializer.Deserialize<StartupOptions>(jsonstr);
                        OptionsLoaded = true;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error loading options {e.Message}");
                        OptionsLoaded = false;
                        startOptions = null;
                    }

                }
                else
                {
                    Console.WriteLine($"startup options file {OptionsFileName} missing.");
                }
            }
        }

    }
}
