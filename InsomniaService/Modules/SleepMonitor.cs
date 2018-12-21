using System;

namespace MadWizard.Insomnia.Modules
{
    class SleepMonitor : ModuleBase
    {
        public DateTime? SuspendTime { get; private set; }

        public TimeSpan Duration { get; private set; }

        public event EventHandler<PowerNapEventArgs> PowerNap;
        public event EventHandler<EventArgs> SleepOver;

        protected override void OnSuspend()
        {
            SuspendTime = DateTime.Now;
        }

        protected override void OnResumeSuspend()
        {
            if (SuspendTime.HasValue)
            {
                DateTime suspend = SuspendTime.Value;

                if (DateTime.Now.Day != suspend.Day)
                {
                    Duration += DateTime.Today - suspend;
                    SleepOver?.Invoke(this, EventArgs.Empty);
                    Duration += DateTime.Now - DateTime.Today;
                }
                else
                {
                    TimeSpan time = DateTime.Now - suspend;
                    PowerNap?.Invoke(this, new PowerNapEventArgs { SleepTime = time });
                    Duration += time;
                }

                SuspendTime = null;
            }
        }

        public void ResetTime()
        {
            Duration = TimeSpan.Zero;
        }
    }

    class PowerNapEventArgs : EventArgs
    {
        public TimeSpan SleepTime { get; internal set; }
    }
}