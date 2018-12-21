using Cassia;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Analyzers
{
    class RemoteDesktop : ModuleBase
    {
        internal RemoteDesktop()
        {
            AddDependency(typeof(IdleScanner));
            AddDependency(typeof(TerminalServer));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            Component<IdleScanner>().Scan += ScanSessions;
        }

        private void ScanSessions(object sender, IdleScanAnalysis analysis)
        {
            foreach (ITerminalServicesSession session in Component<TerminalServer>().Sessions)
                if (session.ConnectionState == ConnectionState.Active)
                    if (session.IsRemoteConnected())
                    {
                        analysis.BusyTokens.Add($"<{session.ClientName}\\{session.UserName}>");
                        analysis.Busy = true;
                    }
        }
    }
}