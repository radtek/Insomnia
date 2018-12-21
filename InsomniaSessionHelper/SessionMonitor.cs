using MadWizard.Insomnia.Modules.Session;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia
{
    class SessionMonitor
    {
        SessionHelper helper;

        Timer timer;

        internal SessionMonitor(SessionHelper helper)
        {
            this.helper = helper;
            this.helper.ConfigChanged += SessionHelper_ConfigChanged;
            this.helper.Termination += SessionHelper_Termination;

            timer = new Timer();
            timer.Elapsed += Timer_Elapsed;
        }

        #region SessionHelper
        private void SessionHelper_ConfigChanged(object sender, ConfigChangedEvent e)
        {
            timer.Interval = e.Config.Interval;
            timer.Enabled = e.Config.MonitorIdleTime;
        }
        private void SessionHelper_Termination(object sender, EventArgs e)
        {
            timer.Stop();
        }
        #endregion

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (helper.Config.MonitorIdleTime)
            {
                Monitor_IdleTime();
            }
        }

        #region Monitor Methods
        private void Monitor_IdleTime()
        {
            helper.SendMessage(new IdleTimeMessage(Win32API.IdleTime));
        }
        #endregion
    }
}