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


        /// <summary>
        /// Initiates a logging instance. This function must be called before any logging can be performed.
        /// It sets up the log output. Supported values are "console" or "file". Default is "console".
        /// It sets up the log level. Supported values are Fatal, Error, Warning, Information, Debug, Verbose
        /// The Logger will rotate log files based on the maximum size specified. So, the oldest file will be deleted 
        /// after creating a new backup of the current log file. A backup of current log file is created when 
        /// the maximum file size (in MB) is reached. This function also allows initialization of the actual log name.
        /// If log file name is "app_log.txt", then the backup files are named as app_log.0.txt, app_log.1.txt...
        /// </summary>
        /// <param name="log_to">Destination for logged data.</param>
        /// <param name="log_level">The log level controlling the amount of logged data.</param>
        /// <param name="max_file_count">If logging to file, the maximum number of backup logs maintained.</param>
        /// <param name="max_file_size_mb">The maximum size in megabytes of each log file.</param>
        /// <param name="log_file_name">The name of the log file to use to store logging data.</param>
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

        /// <summary>
        /// This function controls the actual data that gets written based on the setup log level.
        /// If the log level is set to "Information", any data written to function <c>Verbose</c> will not be logged to destination.
        /// Thus the log output could be varied without recompiling the application. This is called by each of the log functions 
        /// before writing data to the destination. If the function log level is greater than the set level, then the function 
        /// returns enabled as true, or false otherwise.
        /// </summary>
        /// <param name="curr_level">The </param>
        /// <returns>true or false</returns>
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
        /// <summary>
        /// Writes fatal messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Data to be logged as fatal.</param>
        public static void Fatal(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg, contextid);
        }

        /// <summary>
        /// Writes fatal messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="e">Exception that occured when this data was generated.</param>
        /// <param name="msg">Data to be logged as fatal.</param>
        public static void Fatal(Exception e, string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Fatal))
            {
                return;
            }
            Write("[FTL]: ", msg + System.Environment.NewLine + e.Message, contextid);
        }

        /// <summary>
        /// Writes error messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Message to be logged as Error</param>
        public static void Error(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            Write("[ERR]: ", msg, contextid);
        }

        /// <summary>
        /// Writes error messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="e">Exceptiont that occured triggering this error log</param>
        /// <param name="msg">Message to be logged as Error</param>
        public static void Error(Exception e, string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Error))
            {
                return;
            }
            if (e != null)
            {
                // Todo: Write additional exception info along with message
                Write("[ERR]: ", msg + System.Environment.NewLine + e.Message, contextid);
            }
            else
            {
                Write("[ERR]: ", msg, contextid);
            }
        }

        /// <summary>
        /// Writes Warning messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Message to be logged as Warning</param>
        public static void Warning(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Warning))
            {
                return;
            }
            Write("[WRN]: ", msg, contextid);
        }

        /// <summary>
        /// Writes information messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Message to be logged as Information.</param>
        public static void Information(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Information))
            {
                return;
            }
            Write("[INF]: ", msg, contextid);
        }

        /// <summary>
        /// Writes debug messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Message to be logged as Debug</param>
        public static void Debug(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Debug))
            {
                return;
            }
            Write("[DBG]: ", msg, contextid);
        }

        /// <summary>
        /// Writes Verbose messages to the logging destination.
        /// First checks to see if this level is allowed to write to destination.
        /// If yes, writes the logged message to the destination.
        /// </summary>
        /// <param name="msg">Message to be logged as Verbose</param>
        public static void Verbose(string msg, string contextid = "")
        {
            if (!IsEnabled(LogEventLevel.Verbose))
            {
                return;
            }
            Write("[VRB]: ", msg, contextid);
        }


        /// <summary>
        /// This function actually writes data to the destination based on the 
        /// initial setup
        /// </summary>
        /// <param name="loglevelstr">String indicating type of logged message</param>
        /// <param name="msg">Message to be logged</param>
        public static void Write(string loglevelstr, string msg, string contextid)
        {
            string logDateTimeStr = DateTime.Now.ToString();
            if (logTo.ToLower().Equals("file"))
            {
                lock (logFileName) // multiple thread access control to file
                {
                    RollLogs();
                    if (contextid.Length > 0)
                    {
                        File.AppendAllText(logFileName, logDateTimeStr + " " + loglevelstr + "[" + contextid + "]" + msg + Environment.NewLine, Encoding.UTF8);
                    }
                    else
                    {
                        File.AppendAllText(logFileName, logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine, Encoding.UTF8);
                    }
                }
            }
            else
            {
                if (contextid.Length > 0)
                {
                    System.Console.Write(logDateTimeStr + " " + loglevelstr + "[" + contextid + "]" + msg + Environment.NewLine);
                }
                else
                {
                    System.Console.Write(logDateTimeStr + " " + loglevelstr + msg + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// This function is responsible for ensuring that the number of log files do not exceed the limit setup,
        /// and ensure that the oldest log file is removed and the other files renamed appropriately.
        /// </summary>
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
