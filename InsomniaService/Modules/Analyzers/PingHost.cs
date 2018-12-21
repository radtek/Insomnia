using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Analyzers
{
    class PingHost : ModuleBase
    {
        Ping ping;

        string[] hostsAwake;
        string[] hostsWatch;

        internal PingHost()
        {
            AddDependency(typeof(IdleScanner));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            ping = new Ping();

            hostsAwake = ctx.Config.ReadKeyAsCSV("hosts", "awake");
            hostsWatch = ctx.Config.ReadKeyAsCSV("hosts", "watch");

            Component<IdleScanner>().Scan += ScanHosts;
        }

        private void ScanHosts(object sender, IdleScanAnalysis analysis)
        {
            foreach (string host in hostsAwake.Union(hostsWatch))
            {
                try
                {
                    PingReply reply = ping.Send(host);
                    if (reply.Status == IPStatus.Success)
                    {
                        if (hostsAwake.Contains(host))
                        {
                            if (!analysis.BusyTokens.Contains(host))
                                analysis.BusyTokens.Add(host);
                            analysis.Busy = true;
                        }
                        else
                        {
                            analysis.InfoTokens.Add(host);
                        }
                    }
                }
                catch (PingException)
                {

                }
            }
        }
    }
}