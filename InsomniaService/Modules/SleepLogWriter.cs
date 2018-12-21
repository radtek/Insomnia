using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules
{
    class SleepLogWriter : ModuleBase, LogFileSweeper.ISweepable
    {
        FileInfo logFile;

        internal SleepLogWriter()
        {
            AddDependency(typeof(SleepMonitor));
            AddDependency(typeof(IdleScanner));
        }

        DirectoryInfo LogFileSweeper.ISweepable.WatchDirectory
        {
            get
            {
                return Context.LogsDir;
            }
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            logFile = new FileInfo(Path.Combine(ctx.BaseDir.FullName, "sleep.log"));

            Component<IdleScanner>().Prepare += PrepareLog;
            Component<IdleScanner>().Eval += WriteTokens;

            Component<SleepMonitor>().PowerNap += OnPowerNap;
            Component<SleepMonitor>().SleepOver += OnSleepOver;
        }

        private void PrepareLog(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            if (logFile.Exists)
            {
                if (DateTime.Now.Date != logFile.LastWriteTime.Date)
                    ArchiveLog();
                else
                    sb.AppendLine();
            }

            sb.AppendLine("Startup.");
            sb.AppendLine();

            WriteLog(sb.ToString());
        }

        protected override void OnSuspend()
        {
            WriteLog(string.Empty); // ggfs. Tagesabschluss schreiben
        }

        protected override void OnShutdown()
        {
            TimeSpan sleepDuration = Component<SleepMonitor>().Duration;

            var sb = new StringBuilder().AppendLine().Append("Shutdown.");

            if (sleepDuration.TotalMinutes > 0)
            {
                sb.Append(" Total sleep duration: ");
                sb.Append(FormatTimeSpan(sleepDuration));
            }

            WriteLog(sb.ToString());
        }

        private void OnSleepOver(object sender, EventArgs e)
        {
            FinishLog();
        }

        private void OnPowerNap(object sender, PowerNapEventArgs e)
        {
            logFile.Refresh();

            if (logFile.Length > 0)
            {
                var sb = new StringBuilder("zzzZZZzzz... ");
                sb.Append("(");
                sb.Append(FormatTimeSpan(e.SleepTime));
                sb.Append(")");
                sb.AppendLine();

                WriteLog(sb.ToString());
            }
        }

        private void WriteTokens(object sender, IdleScanAnalysis analysis)
        {
            var sb = new StringBuilder(DateTime.Now.ToString("HH:mm")).Append("\t");

            if (analysis.BusyTokens.Count > 0)
                sb.Append(string.Join(", ", analysis.BusyTokens));
            else
                sb.Append("-");

            if (analysis.InfoTokens.Count > 0)
                sb.Append(" ").Append("[").Append(string.Join(", ", analysis.InfoTokens)).Append("]");

            if (analysis.Error)
                sb.Append(" -> ERROR");

            sb.AppendLine();

            WriteLog(sb.ToString());
        }

        private void WriteLog(string text)
        {
            logFile.Refresh();

            bool create = !logFile.Exists;

            if (!create)
            {
                if (logFile.LastWriteTime.Day != DateTime.Now.Day)
                {
                    FinishLog();

                    create = true;
                }
            }

            File.AppendAllText(logFile.FullName, text);

            if (create)
            {
                logFile.CreationTime = DateTime.Now;
            }
        }

        private void FinishLog()
        {
            var sb = new StringBuilder().AppendLine();
            sb.Append("Midnight! Total sleep duration: ");
            sb.Append(FormatTimeSpan(Component<SleepMonitor>().Duration));
            File.AppendAllText(logFile.FullName, sb.ToString());

            ArchiveLog();

            Component<SleepMonitor>().ResetTime();
        }

        private void ArchiveLog()
        {
            logFile.Refresh();

            string logs = Context.LogsDir.FullName;
            string name = logFile.CreationTime.ToString("yyyy-MM-dd") + ".log";
            string path = Path.Combine(logs, name);

            // Manchmal bleibt die Datei hängen.
            if (File.Exists(path))
            {
                File.Delete(path);

                EventLog.WriteEntry("Archive-File overwritten", EventLogEntryType.Warning);
            }

            File.Move(logFile.FullName, path);
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            var sb = new StringBuilder();
            if (time.Days > 0)
                sb.Append(time.ToString("%d")).Append(" day(s), ");
            sb.Append(time.ToString(@"hh\:mm")).Append(" h");
            return sb.ToString();
        }
    }
}