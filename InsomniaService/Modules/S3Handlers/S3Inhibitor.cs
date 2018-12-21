using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static MadWizard.Insomnia.Tools.Win32API;

namespace MadWizard.Insomnia.Modules.S3Handlers
{
    class S3Inhibitor : ModuleBase
    {
        internal S3Inhibitor()
        {
            AddDependency(typeof(IdleScanner));
        }

        internal PowerRequest Request { get; private set; }

        protected override void OnActivate(ServiceContext ctx)
        {
            Component<IdleScanner>().Idle += OnIdle;
            Component<IdleScanner>().Busy += OnBusy;
        }

        private void OnIdle(object sender, IdleScanAnalysis analysis)
        {
            Request?.Clear();
            Request = null;
        }

        private void OnBusy(object sender, IdleScanAnalysis analysis)
        {
            string tokens = string.Join(", ", analysis.BusyTokens.Where(t => !(t.StartsWith("((") && t.EndsWith("))"))));
            string reason = $"Kein Standby-Modus wegen: {tokens}";

            if (Request?.Reason != reason)
            {
                Request?.Clear();
                Request = new PowerRequest(reason);
            }
        }

        protected override void OnShutdown()
        {
            Request?.Clear();
            Request = null;
        }
    }
}