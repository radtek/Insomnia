using Cassia;
using MadWizard.Insomnia.Tools;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Session
{
    class SessionHelper : ModuleBase
    {
        public const int DEFAULT_TIMEOUT = 5000;

        List<SessionHelperInstance> instances;

        NamedPipeServer<Message> pipeServer;

        public SessionHelper()
        {
            AddDependency(typeof(TerminalServer));
        }

        public bool AutoStart { get; set; } = false;

        public SessionHelperInstance[] Instances
        {
            get
            {
                lock (instances)
                {
                    return instances.Where(i => i.Process != null).ToArray();
                }
            }
        }

        public event SessionHelperEventHandler Started;
        public event SessionHelperEventHandler Terminated;

        protected override void OnActivate(ServiceContext ctx)
        {
            instances = new List<SessionHelperInstance>();

            Component<TerminalServer>().UserLogin += OnUserLogin;
        }

        protected override void OnStart()
        {
            pipeServer = new NamedPipeServer<Message>(Message.PIPE_NAME);
            pipeServer.ClientDisconnected += PipeServer_ClientDisconnected;
            pipeServer.ClientConnected += PipeServer_ClientConnected;
            pipeServer.Start();

            if (AutoStart)
            {
                LaunchHelpers();
            }
        }

        protected override void OnShutdown()
        {
            TerminateHelpers(DEFAULT_TIMEOUT, true);

            pipeServer.Stop();
        }

        private void OnUserLogin(object sender, UserLoginEventArgs e)
        {
            int sid = e.Session.SessionId;

            if (InstanceBySID(sid) == null && AutoStart)
            {
                LaunchHelper(sid);
            }
        }

        #region PipeServer
        private void PipeServer_ClientConnected(NamedPipeConnection<Message, Message> connection)
        {
            SessionHelperInstance instance = new SessionHelperInstance(connection);
            instance.Started += Instance_Started;
            instance.Terminated += Instance_Terminated;

            // Initiale Konfiguration
            SessionHelperConfig config = new SessionHelperConfig();
            config.Interval = Context.Interval / 10;
            instance.Config = config;
        }
        private void Instance_Started(SessionHelperInstance instance)
        {
            if (InstanceBySID(instance.SID) != null)
            {
                EventLog.WriteEntry($"SessionHelper redundant (SID={instance.SID}, PID={instance.PID})", EventLogEntryType.Warning, 81);

                instance.Terminate();

                return;
            }

            if (Context.DebugLog)
                EventLog.WriteEntry($"SessionHelper connected (SID={instance.SID}, PID={instance.PID})", EventLogEntryType.Information, 81);

            lock (instances)
            {
                instances.Add(instance);
            }

            Started?.Invoke(instance);
        }
        private void Instance_Terminated(SessionHelperInstance instance, bool forced)
        {
            if (forced)
                EventLog.WriteEntry($"SessionHelper terminated (SID={instance.SID}, PID={instance.PID}) = hung", EventLogEntryType.Error, 82);

            instance.Terminated -= Instance_Terminated;
            instance.Started -= Instance_Started;

            lock (instances)
            {
                instances.Remove(instance);
            }

            Terminated?.Invoke(instance);
        }
        private void PipeServer_ClientDisconnected(NamedPipeConnection<Message, Message> connection)
        {
            lock (instances)
                foreach (SessionHelperInstance instance in instances)
                    if (instance.pipe == connection)
                        if (Context.DebugLog && !instance.forcedKill)
                            EventLog.WriteEntry($"SessionHelper disconnected (SID={instance.SID}, PID={instance.PID})", EventLogEntryType.Information, 82);
        }
        private void PipeServer_Error(Exception exception)
        {
            EventLog.WriteEntry(exception.ToString(), EventLogEntryType.Error);
        }
        #endregion

        public void LaunchHelpers(int? timeout = DEFAULT_TIMEOUT)
        {
            foreach (ITerminalServicesSession session in Component<TerminalServer>().Sessions)
                if (session.UserAccount != null)
                    LaunchHelper(session.SessionId, timeout);
        }

        public SessionHelperInstance LaunchHelper(int? sid = null, int? timeout = DEFAULT_TIMEOUT)
        {
            var args = new StringBuilder();
            if (Context.StartupDelay > 0)
            {
                args.Append($" -StartupDelay={Context.StartupDelay}");

                if (timeout != null)
                    timeout += Context.StartupDelay;
            }
            if (Context.DebugLog)
                args.Append($" -DebugLog");

            int pid = Win32API.CreateProcessInSession($"InsomniaSessionHelper.exe {args}", (uint)(sid != null ? sid : 0));

            if (Context.DebugLog)
                EventLog.WriteEntry($"SessionHelper started (PID={pid})", EventLogEntryType.Information, 80);

            int time = 0;
            while (timeout == null || time < timeout)
            {
                if (InstanceByPID(pid) != null)
                    return InstanceByPID(pid);

                Thread.Sleep(100);

                time += 100;
            }

            throw new TimeoutException();
        }

        public SessionHelperInstance InstanceByPID(int pid)
        {
            try
            {
                return Instances.First(i => i.PID == pid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        public SessionHelperInstance InstanceBySID(int? sid)
        {
            if (sid == null)
                sid = Component<TerminalServer>()[sid].SessionId;

            try
            {
                return Instances.First(i => i.SID == sid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public void SendMessage(Message message)
        {
            lock (instances)
            {
                foreach (SessionHelperInstance instance in instances)
                {
                    instance.SendMessage(message);
                }
            }
        }

        public void TerminateHelpers(double? timeout = null, bool wait = false)
        {
            foreach (SessionHelperInstance instance in Instances)
            {
                instance.Terminate(timeout, wait);
            }
        }
    }

    delegate void SessionHelperEventHandler(SessionHelperInstance helper);
}