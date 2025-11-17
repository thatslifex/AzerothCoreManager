using NetSparkleUpdater;
using NetSparkleUpdater.Interfaces;
using System;
using System.IO;

namespace AzerothCoreManager
{
    public class SparkleFileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();

        public SparkleFileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
        }

        public void PrintMessage(string format, params object[] args)
        {
            WriteLog(string.Format(format, args));
        }

        public void PrintError(string format, params object[] args)
        {
            WriteLog("ERROR: " + string.Format(format, args));
        }

        private void WriteLog(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath,
                        $"{DateTime.Now:O} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // optional: Fehler schlucken oder weiterleiten
            }
        }
    }
}
