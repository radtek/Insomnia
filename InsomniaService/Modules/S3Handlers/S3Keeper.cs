using MadWizard.Insomnia.Events;
using MadWizard.Insomnia.Modules.Session;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia.Modules.S3Handlers
{
    class S3Keeper : ModuleBase
    {
        Timer timer;

        SessionHelperInvocation helper;

        internal S3Keeper()
        {
            AddDependency(typeof(TerminalServer));
            AddDependency(typeof(SessionHelper));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            Component<TerminalServer>().UserLogin += OnUserDetected;

            timer = new Timer();
            timer.AutoReset = false;
            timer.Interval = ctx.Interval;
            timer.Elapsed += OnTimerElapsed;
        }

        protected override void OnResumeSuspend()
        {
            TerminalServer wts = Component<TerminalServer>();

            if (wts.ConsoleActive && !wts.ConsoleLocked)
            {
                //= keine Kennworteingabe nach dem Aufwachen

                try
                {
                    helper = new SessionHelperInvocation(Component<SessionHelper>());
                    helper.UserDetected += OnUserDetected;
                }
                catch (Exception e)
                {
                    EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 2);

                    return;
                }
            }

            timer.Start();
        }

        protected override void OnShutdown()
        {
            Stop();
        }

        private void OnUserDetected(object sender, EventArgs e)
        {
            if (timer.Enabled)
            {
                Stop();

                if (Context.EventLog)
                    EventLog.WriteEntry("User present", EventLogEntryType.Information, 2);

                PublishEvent(new UserPresentEvent());
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Stop();

            if (Component<S3Inhibitor>()?.Request != null)
            {
                if (Context.EventLog)
                    EventLog.WriteEntry("Computer busy", EventLogEntryType.Information, 2);

                return;
            }

            if (Context.EventLog)
                EventLog.WriteEntry("User not present", EventLogEntryType.Warning, 2);

            Win32API.EnterStandby();
        }

        private void Stop()
        {
            if (helper != null)
            {
                helper.UserDetected -= OnUserDetected;
                helper.Finish();
                helper = null;
            }

            timer.Stop();
        }

        private class SessionHelperInvocation
        {
            SessionHelperInstance helper;
            SessionHelperConfig previous;
            bool terminate;

            long? idleTime;

            internal SessionHelperInvocation(SessionHelper sh, int? sid = null)
            {
                if ((helper = sh.InstanceBySID(sid)) == null)
                {
                    helper = sh.LaunchHelper(sid);

                    terminate = true;
                }

                previous = helper.Config;

                // IdleTime-Monitor einschalten
                SessionHelperConfig c = previous;
                c.MonitorIdleTime = true;
                helper.Config = c;

                helper.MessageArrived += MessageArrived;
            }

            internal event EventHandler<EventArgs> UserDetected;

            private void MessageArrived(SessionHelperInstance helper, Message message)
            {
                if (message is IdleTimeMessage)
                {
                    IdleTimeMessage itm = (IdleTimeMessage)message;

                    if (idleTime != null && idleTime > itm.Time)
                    {
                        UserDetected?.Invoke(this, EventArgs.Empty);
                    }

                    idleTime = itm.Time;
                }
            }

            internal void Finish()
            {
                if (helper != null)
                {
                    helper.MessageArrived -= MessageArrived;

                    // IdleTime-Monitor ggfs. ausschalten
                    SessionHelperConfig c = helper.Config;
                    c.MonitorIdleTime = previous.MonitorIdleTime;
                    helper.Config = c;

                    if (terminate)
                    {
                        helper.Terminate(SessionHelper.DEFAULT_TIMEOUT);
                    }

                    helper = null;
                }
            }
        }
    }
}