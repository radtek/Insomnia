using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;

namespace MadWizard.Insomnia.Modules
{
    class IdleScanner : ModuleBase
    {
        const int INITAL_DELAY = 5000;

        private Timer timer;

        public int IdleCount { get; private set; }

        public event EventHandler<EventArgs> Prepare;

        public event EventHandler<IdleScanAnalysis> Scan;
        public event EventHandler<IdleScanAnalysis> Idle;
        public event EventHandler<IdleScanAnalysis> Busy;
        public event EventHandler<IdleScanAnalysis> Eval;

        protected override void OnActivate(ServiceContext ctx)
        {
            if (ctx.Interval > 0.0)
            {
                timer = new Timer();
                timer.Interval = ctx.Interval;
                timer.Elapsed += OnTimerElapsed;
            }
        }

        protected override void OnStart()
        {
            Prepare?.Invoke(this, EventArgs.Empty);

            OnTimerStart();
        }
        protected override void OnSuspend()
        {
            OnTimerStop();
        }
        protected override void OnResumeSuspend()
        {
            OnTimerStart();
        }
        protected override void OnShutdown()
        {
            OnTimerStop();
        }

        private void OnTimerStart()
        {
            if (timer != null && Scan != null)
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(INITAL_DELAY);

                    OnTimerElapsed(timer, null);

                    timer.Start();
                });
            }
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs args)
        {
            try
            {
                IdleScanAnalysis analysis = new IdleScanAnalysis(this);

                Scan?.Invoke(this, analysis);

                try
                {
                    if (analysis.Busy)
                    {
                        IdleCount = 0;

                        Busy?.Invoke(this, analysis);
                    }
                    else
                    {
                        IdleCount++;

                        Idle?.Invoke(this, analysis);
                    }
                }
                catch (Exception e)
                {
                    EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 18);

                    analysis.Error = true;
                }

                Eval?.Invoke(this, analysis);
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 19);
            }
        }

        private void OnTimerStop()
        {
            timer?.Stop();

            IdleCount = 0;
        }
    }

    public class IdleScanAnalysis : EventArgs
    {
        private IdleScanner scanner;

        public bool Busy { get; set; }
        public bool Error { get; set; }

        public List<string> BusyTokens { get; }
        public List<string> InfoTokens { get; }

        public bool Idle => !Busy;
        public int IdleCount => scanner.IdleCount;

        internal IdleScanAnalysis(IdleScanner scanner)
        {
            this.scanner = scanner;

            BusyTokens = new List<string>();
            InfoTokens = new List<string>();
        }
    }
}