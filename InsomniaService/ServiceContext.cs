using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    class ServiceContext
    {
        public readonly DirectoryInfo BaseDir;
        public readonly DirectoryInfo LogsDir;

        public INIFile Config { get; private set; }

        public int StartupDelay { get; private set; }
        public double Interval { get; private set; }

        public bool DebugLog;
        public bool EventLog;

        internal ServiceContext()
        {
            BaseDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Locati‌​on));
            LogsDir = new DirectoryInfo(Path.Combine(BaseDir.FullName, "logs"));
        }

        internal void LoadConfig()
        {
            Config = new INIFile(Path.Combine(BaseDir.FullName, "service.ini"));

            StartupDelay = int.Parse(Config.ReadKey("config", "startupdelay", "0"));
            Interval = double.Parse(Config.ReadKey("config", "interval", "-1"));

            DebugLog = bool.Parse(Config.ReadKey("config", "debuglog", "false"));
            EventLog = bool.Parse(Config.ReadKey("config", "eventlog", "false"));
        }
    }
}