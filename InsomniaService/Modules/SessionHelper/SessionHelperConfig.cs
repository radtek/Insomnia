using MadWizard.Insomnia.Modules.S3Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Session
{
    [Serializable]
    public struct SessionHelperConfig
    {
        public double Interval { get; set; }

        public bool MonitorIdleTime { get; set; }

        public bool DisplayNotifyIcon { get; set; }

        public SessionHelperWakeOptions? WakeOptions { get; set; }

        [Serializable]
        public struct SessionHelperWakeOptions
        {
            public WakeHost[] WakeHosts { get; set; }

            public bool ResolveIP { get; set; }
        }
    }
}