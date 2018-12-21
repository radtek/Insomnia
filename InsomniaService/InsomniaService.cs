using Cassia;
using MadWizard.Insomnia.Exceptions;
using MadWizard.Insomnia.Modules;
using MadWizard.Insomnia.Modules.Analyzers;
using MadWizard.Insomnia.Modules.Bugfix;
using MadWizard.Insomnia.Modules.S3Handlers;
using MadWizard.Insomnia.Modules.Test;
using MadWizard.Insomnia.Modules.UI;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;

namespace MadWizard.Insomnia
{
    public partial class InsomniaService : ServiceBase
    {
        internal ServiceContext ctx;

        IList<ModuleBase> modules;

        public InsomniaService()
        {
            InitializeComponent();
            InitializeEventLog();
            InitializeContext();

            CanHandlePowerEvent = true;
            CanHandleSessionChangeEvent = true;
            CanShutdown = true;

            modules = new List<ModuleBase>();
        }

        internal ModuleBase this[Type type]
        {
            get
            {
                foreach (ModuleBase module in modules)
                    if (module.GetType() == type)
                        return module;

                return null;
            }
        }

        internal ModuleBase[] Modules
        {
            get
            {
                return modules.ToArray();
            }
        }

        internal event EventHandler<ActivationEvent> ModuleActivation;
        internal event EventHandler<EventBase> ModuleEvent;

        internal event EventHandler<EventArgs> Startup;
        internal event EventHandler<PowerChangeEvent> PowerChange;
        internal event EventHandler<SessionChangeEvent> SessionChange;
        internal event EventHandler<EventArgs> Shutdown;

        #region Initializers
        private void InitializeEventLog()
        {
            AutoLog = false;

            (EventLog as ISupportInitialize).BeginInit();
            if (!EventLog.SourceExists(ServiceName))
            {
                EventLog.CreateEventSource(ServiceName, "Application");
            }
            (EventLog as ISupportInitialize).EndInit();

            EventLog.Source = ServiceName;
            EventLog.Log = "Application";
        }
        private void InitializeContext()
        {
            ctx = new ServiceContext();

            Directory.SetCurrentDirectory(ctx.BaseDir.FullName);
            Directory.CreateDirectory(ctx.LogsDir.FullName);
        }
        #endregion

        private void LoadConfig()
        {
            ctx.LoadConfig();

            // Scanners
            if (bool.Parse(ctx.Config.ReadKey("scan", "requests", "false")))
                AddModule(new PowerRequests());
            if (bool.Parse(ctx.Config.ReadKey("scan", "idletime", "false")))
                AddModule(new IdleTime());
            if (bool.Parse(ctx.Config.ReadKey("scan", "rdpcheck", "false")))
                AddModule(new RemoteDesktop());
            if (bool.Parse(ctx.Config.ReadKey("scan", "pinghost", "false")))
                AddModule(new PingHost());
            if (bool.Parse(ctx.Config.ReadKey("scan", "keepwake", "false")))
                AddModule(new ReversePing());

            // S3-Handlers
            if (bool.Parse(ctx.Config.ReadKey("s3", "keeper", "false")))
                AddModule(new S3Keeper());
            if (bool.Parse(ctx.Config.ReadKey("s3", "enforcer", "false")))
                AddModule(new S3Enforcer());
            if (bool.Parse(ctx.Config.ReadKey("s3", "inhibitor", "false")))
                AddModule(new S3Inhibitor());
            {
                string mode = ctx.Config.ReadKey("s3", "terminator", "false");

                if (mode.Equals("true") || mode.Equals("continuous"))
                    AddModule(new S3Terminator(mode.Equals("continuous")));
            }

            // User-Interface
            if (bool.Parse(ctx.Config.ReadKey("ui", "traymenu", "false")))
                AddModule(new TrayMenuController());

            // Logging
            if (bool.Parse(ctx.Config.ReadKey("config", "sleeplog", "false")))
                AddModule(new SleepLogWriter());
            if (int.Parse(ctx.Config.ReadKey("config", "logcount", "-1")) > -1)
                AddModule(new LogFileSweeper());

            // Bugfixes
            if (bool.Parse(ctx.Config.ReadKey("bugfix", "PowerBroadcast", "false")))
                AddModule(new PowerBroadcastFallback()); // "Windows Ninja Wakeup"

            // Test Modules
            if (bool.Parse(ctx.Config.ReadKey("test", "sessionhelper", "false")))
                AddModule(new TestSessionHelper());
        }

        private void AddModule(ModuleBase module)
        {
            Type type = module.GetType();
            if (this[type] != null)
                throw new InvalidOperationException("Duplicate Module");

            foreach (Type depType in module.Dependencies)
            {
                if (this[depType] == null)
                {
                    ModuleBase dependency = (ModuleBase)Activator.CreateInstance(depType);

                    AddModule(dependency);
                }
            }

            if (this[type] != null)
                throw new InvalidOperationException("Circular Dependency detected");

            module.Service = this;

            modules.Add(module);
        }

        internal void SendEvent(ModuleBase source, EventBase e)
        {
            ModuleEvent?.Invoke(source, e);
        }

        internal void SendEvent(ModuleBase source, PowerChangeEvent e)
        {
            PowerChange?.Invoke(source, e);
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                LoadConfig();

                if (ctx.StartupDelay > 0)
                    Thread.Sleep(ctx.StartupDelay);

                ModuleActivation?.Invoke(this, new ActivationEvent { Context = ctx });

                Startup?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 99);

                throw;
            }
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus status)
        {
            try
            {
                if (ctx.EventLog)
                {
                    if (status == PowerBroadcastStatus.Suspend)
                        EventLog.WriteEntry("Entering Standby");
                    if (status == PowerBroadcastStatus.ResumeSuspend)
                    {
                        if (this[typeof(PowerBroadcastFallback)] != null)
                            return true;

                        EventLog.WriteEntry("Resuming Operation");
                    }
                }

                PowerChange?.Invoke(this, new PowerChangeEvent { Status = status });
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 99);
            }

            return true;
        }

        protected override void OnSessionChange(SessionChangeDescription desc)
        {
            try
            {
                SessionChange?.Invoke(this, new SessionChangeEvent { Description = desc });
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 99);
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
        }

        protected override void OnStop()
        {
            try
            {
                Shutdown.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 99);
            }
        }
    }

    class ActivationEvent : EventArgs
    {
        internal ServiceContext Context { get; set; }
    }
    class PowerChangeEvent : EventArgs
    {
        internal PowerBroadcastStatus Status { get; set; }
    }
    class SessionChangeEvent : EventArgs
    {
        internal SessionChangeDescription Description { get; set; }
    }
}