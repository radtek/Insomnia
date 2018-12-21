using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia.Modules.Bugfix
{
    class PowerBroadcastFallback : ModuleBase
    {
        const double FALLBACK_INTERVAL = 1000;

        Timer fallbackTimer;
        DateTime lastTime;

        public bool RaisedEvent { get; private set; }

        protected override void OnActivate(ServiceContext ctx)
        {
            fallbackTimer = new Timer();
            fallbackTimer.AutoReset = true;
            fallbackTimer.Interval = FALLBACK_INTERVAL;
            fallbackTimer.Elapsed += OnFallbackTimerElapsed;
        }

        protected override void OnSuspend()
        {
            RaisedEvent = false;

            lastTime = DateTime.Now;

            fallbackTimer.Start();
        }

        private void OnFallbackTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if ((DateTime.Now - lastTime).TotalMilliseconds > 2 * FALLBACK_INTERVAL)
            {
                if (Context.EventLog)
                {
                    EventLog.WriteEntry("Resuming Operation [FAKE]");
                }

                RaisedEvent = true;

                Service.SendEvent(this, new PowerChangeEvent { Status = PowerBroadcastStatus.ResumeSuspend });
            }

            lastTime = DateTime.Now;
        }

        protected override void OnResumeSuspend()
        {
            fallbackTimer.Stop();
        }
    }
}