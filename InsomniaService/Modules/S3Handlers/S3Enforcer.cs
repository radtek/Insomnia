using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.S3Handlers
{
    class S3Enforcer : ModuleBase
    {
        private int idleMax;

        internal S3Enforcer()
        {
            AddDependency(typeof(IdleScanner));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            idleMax = int.Parse(ctx.Config.ReadKey("s3", "idlemax", "1"));

            Component<IdleScanner>().Idle += OnIdle;
        }

        private void OnIdle(object sender, IdleScanAnalysis analysis)
        {
            if (analysis.IdleCount > idleMax)
            {
                if (Context.DebugLog)
                    EventLog.WriteEntry("Computer idle", EventLogEntryType.Information, 5);

                Win32API.EnterStandby();
            }
        }
    }
}