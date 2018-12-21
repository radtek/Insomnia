using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Analyzers
{
    class PowerRequests : ModuleBase, LogFileSweeper.ISweepable
    {
        DirectoryInfo requestsDir;

        IDictionary<string, string[]> selectors;

        bool keepAlive;

        internal PowerRequests()
        {
            AddDependency(typeof(IdleScanner));
        }

        DirectoryInfo LogFileSweeper.ISweepable.WatchDirectory
        {
            get
            {
                return requestsDir;
            }
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            requestsDir = new DirectoryInfo(Path.Combine(ctx.LogsDir.FullName, "requests"));

            bool logRequestsIfIdle = false;
            selectors = new Dictionary<string, string[]>();
            foreach (string key in ctx.Config.ListKeys("requests"))
                if (key.Equals("%LogIfIdle%", StringComparison.InvariantCultureIgnoreCase))
                    logRequestsIfIdle = bool.Parse(ctx.Config.ReadKey("requests", key));
                else if (key.Equals("%KeepAlive%", StringComparison.InvariantCultureIgnoreCase))
                    keepAlive = bool.Parse(ctx.Config.ReadKey("requests", key));
                else
                    selectors.Add(key, ctx.Config.ReadKeyAsCSV("requests", key));

            Component<IdleScanner>().Scan += AnalyzePowerRequests;

            if (logRequestsIfIdle)
                Component<IdleScanner>().Idle += SavePowerRequests;
        }

        private void AnalyzePowerRequests(object sender, IdleScanAnalysis analysis)
        {
            string output = QueryPowerRequests();

            var detectedRequests = new HashSet<string>();
            foreach (string key in selectors.Keys)
                foreach (string keyWord in selectors[key])
                    if (output.IndexOf(keyWord, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        string token = "((" + key + "))";

                        if (!analysis.BusyTokens.Contains(token))
                            analysis.BusyTokens.Add(token);

                        if (keepAlive)
                            analysis.Busy = true;
                    }
        }

        private void SavePowerRequests(object sender, IdleScanAnalysis analysis)
        {
            try
            {
                var now = DateTime.Now;
                string name = now.ToString("HHmm") + ".log";
                string today = Path.Combine(requestsDir.FullName, now.ToString("yyyy-MM-dd"));
                string path = Path.Combine(today, name);
                Directory.CreateDirectory(today);
                File.AppendAllText(path, QueryPowerRequests());
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error);
            }
        }

        private static string QueryPowerRequests()
        {
            Process process = new Process();
            process.StartInfo.FileName = @"powercfg";
            process.StartInfo.Arguments = "-requests";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();

            return process.StandardOutput.ReadToEnd();
        }
    }
}