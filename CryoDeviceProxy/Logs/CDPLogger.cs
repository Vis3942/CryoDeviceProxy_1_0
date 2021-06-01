using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CryoDeviceProxy.Logs
{
    /// <summary>
    /// Specifies the meaning and relative importance of a log event.
    /// </summary>
    public enum LogEventLevel
    {
        /// <summary>
        /// Anything and everything you might want to know about
        /// a running block of code.
        /// </summary>
        Verbose,

        /// <summary>
        /// Internal system events that aren't necessarily
        /// observable from the outside.
        /// </summary>
        Debug,

        /// <summary>
        /// normal information
        /// </summary>
        Information,

        /// <summary>
        /// Service is degraded or endangered.
        /// </summary>
        Warning,

        /// <summary>
        /// Functionality is unavailable, invariants are broken
        /// or data is lost.
        /// </summary>
        Error,

        /// <summary>
        /// includes notification.. not implemented yet
        /// </summary>
        Fatal
    }
    public class CDPLogger
    {
        private static string logFileName = @"cm_app_log.txt";
        private static int maxRolledLogCount = 5;
        private static int maxLogSize = 1 * 1024 * 1024; // 1 * 1024 * 1024;  1M-10M
        private static string logTo = "file";
        private static LogEventLevel minimumLevel = LogEventLevel.Debug;
        private static string currentExecDirectory = null;
        private static string currentFileWithPath = null;
        public static void InitLogger(string log_to = "console", string log_level = "information",
            int max_file_count = 5, float max_file_size_mb = 1.0f, string log_file_name = "app_log.txt")
        {
            logTo = log_to;
            var loglevel_set = log_level;
            switch (loglevel_set)
            {
                case "verbose":
                    minimumLevel = LogEventLevel.Verbose;
                    break;
                case "debug":
                    minimumLevel = LogEventLevel.Debug;
                    break;
                case "information":
                    minimumLevel = LogEventLevel.Information;
                    break;
                case "warning":
                    minimumLevel = LogEventLevel.Warning;
                    break;
                case "error":
                    minimumLevel = LogEventLevel.Error;
                    break;
                case "fatal":
                    minimumLevel = LogEventLevel.Fatal;
                    break;
            }
            maxRolledLogCount = max_file_count;
            maxLogSize = (int)(max_file_size_mb * 1024 * 1024);
            logFileName = log_file_name;
        }

        private static bool IsEnabled(LogEventLevel curr_level)
        {
            if ((int)curr_level >= (int)minimumLevel)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public static void Fatal(string msg)
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg);
        }
        public static void Fatal(Exception e, string msg)
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg + System.Environment.NewLine + e.Message);
        }

        public static void Error(string msg)
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            Write("[ERR]: ", msg);
        }

        public static void Error(Exception e, string msg)
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            if (e != null)
            {
                // Todo: Write additional exception info along with message
                Write("[ERR]: ", msg + System.Environment.NewLine + e.Message);
            }
            else
            {
                Write("[ERR]: ", msg);
            }
        }

        public static void Warning(string msg)
        {
            if (!IsEnabled(LogEventLevel.Warning))
            {
                return;
            }
            Write("[WRN]: ", msg);
        }

        public static void Information(string msg)
        {
            if (!IsEnabled(LogEventLevel.Information))
            {
                return;
            }
            Write("[INF]: ", msg);
        }

        public static void Debug(string msg)
        {
            if (!IsEnabled(LogEventLevel.Debug))
            {
                return;
            }
            Write("[DBG]: ", msg);
        }

        public static void Verbose(string msg)
        {
            if (!IsEnabled(LogEventLevel.Verbose))
            {
                return;
            }
            Write("[VRB]: ", msg);
        }



        public static void Write(string loglevelstr, string msg)
        {
            string logDateTimeStr = DateTime.Now.ToString();
            if (logTo.ToLower().Equals("file"))
            {
                lock (logFileName) // multiple thread access control to file
                {
                    RollLogs();
                    File.AppendAllText(logFileName, logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            else
            {
                System.Console.Write(logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine);
            }
        }

        private static void RollLogs()
        {
            try
            {
                if (currentFileWithPath == null)
                {
                    if (currentExecDirectory == null)
                    {
                        currentExecDirectory = Environment.CurrentDirectory;
                    }
                    currentFileWithPath = System.IO.Path.Combine(currentExecDirectory, logFileName);
                }
                if (!System.IO.File.Exists(currentFileWithPath))
                {
                    return;
                }
                var length = new FileInfo(currentFileWithPath).Length;
                if (length > maxLogSize)
                {
                    var path = Path.GetDirectoryName(currentFileWithPath);
                    var wildLogName = Path.GetFileNameWithoutExtension(currentFileWithPath) + "*" + Path.GetExtension(currentFileWithPath);
                    var bareLogFilePath = Path.Combine(path, Path.GetFileNameWithoutExtension(currentFileWithPath));
                    string[] logFileList = Directory.GetFiles(path, wildLogName, SearchOption.TopDirectoryOnly);
                    if (logFileList.Length > 0)
                    {
                        // only take files like logfilename.log and logfilename.0.log, so there also can be a maximum of 10 additional rolled files (0..9)
                        var rolledLogFileList = logFileList.Where(fileName => fileName.Length == (currentFileWithPath.Length + 2)).ToArray();
                        Array.Sort(rolledLogFileList, 0, rolledLogFileList.Length);
                        if (rolledLogFileList.Length >= maxRolledLogCount)
                        {
                            File.Delete(rolledLogFileList[maxRolledLogCount - 1]);
                            var list = rolledLogFileList.ToList();
                            list.RemoveAt(maxRolledLogCount - 1);
                            rolledLogFileList = list.ToArray();
                        }
                        // move remaining rolled files
                        for (int i = rolledLogFileList.Length; i > 0; --i)
                            File.Move(rolledLogFileList[i - 1], bareLogFilePath + "." + i + Path.GetExtension(currentFileWithPath), true);
                        var targetPath = bareLogFilePath + ".0" + Path.GetExtension(currentFileWithPath);
                        // move original file
                        File.Move(currentFileWithPath, targetPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
