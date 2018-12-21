using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Analyzers
{
    class ReversePing : ModuleBase
    {
        UdpClient upd;

        IDictionary<string, DateTime> lastSeen;

        internal ReversePing()
        {
            lastSeen = new Dictionary<string, DateTime>();

            AddDependency(typeof(IdleScanner));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            Component<IdleScanner>().Scan += CheckLastSeen;
        }

        protected override void OnStart()
        {
            int port = int.Parse(Context.Config.ReadKey("S3", "wakeport", 1473));

            upd = new UdpClient(port);

            Task.Run(() => Listen());
        }

        protected override void OnShutdown()
        {
            upd?.Close();
            upd = null;
        }

        private async void Listen()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult result = await upd.ReceiveAsync();

                    IPAddress ip = result.RemoteEndPoint.Address;

                    string host;
                    try
                    {
                        IPHostEntry hostEntry = Dns.GetHostEntry(ip);

                        host = hostEntry.HostName.Split('.')[0];
                    }
                    catch
                    {
                        host = ip.ToString();
                    }

                    if (Context.DebugLog)
                        EventLog.WriteEntry($"Received WOL <- {host}", EventLogEntryType.Information, 31);

                    lock (lastSeen)
                    {
                        lastSeen[host] = DateTime.Now;
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 99);
            }
        }

        private void CheckLastSeen(object sender, IdleScanAnalysis analysis)
        {
            lock (lastSeen)
            {
                foreach (string host in lastSeen.Keys)
                {
                    TimeSpan time = DateTime.Now - lastSeen[host];

                    if (time.TotalMilliseconds < Context.Interval)
                    {
                        if (!analysis.BusyTokens.Contains(host))
                            analysis.BusyTokens.Add(host);
                        analysis.Busy = true;
                    }
                }
            }
        }
    }
}