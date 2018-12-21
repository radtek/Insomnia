using Cassia;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules
{
    class TerminalServer : ModuleBase
    {
        private ITerminalServicesManager wtsManager;
        private ITerminalServer wtsServer;

        public bool ConsoleLocked { get; private set; }
        public bool ConsoleActive => this[null].ConnectionState == ConnectionState.Active;

        public ITerminalServicesSession[] Sessions => wtsServer.GetSessions().ToArray();

        public ITerminalServicesSession this[int? sid] => sid != null ? wtsServer.GetSession(sid.Value) : wtsServer.GetConsoleSession();

        public event EventHandler<UserLoginEventArgs> UserLogin;

        protected override void OnActivate(ServiceContext ctx)
        {
            wtsManager = new TerminalServicesManager();
            wtsServer = wtsManager.GetLocalServer();
        }

        protected override void OnPowerChange(PowerChangeEvent e)
        {
            ITerminalServicesSession consoleSession = this[null];

            string clientUser = consoleSession.GetClientUser();

            if (Context.DebugLog)
            {
                var sb = new StringBuilder();
                sb.Append($"PowerEvent: Status={e.Status} ");

                sb.Append("(");
                sb.Append($"Console: SessionId={consoleSession.SessionId}, ");
                if (clientUser.Length > 0)
                    sb.Append($"User={clientUser}, ");
                sb.Append($"State={consoleSession.ConnectionState}");
                if (consoleSession.ConnectionState == Cassia.ConnectionState.Active)
                    sb.Append($"|{(ConsoleLocked ? "Locked" : "Unlocked")}");
                sb.Append(")");

                EventLog.WriteEntry(sb.ToString(), EventLogEntryType.Information, 90);
            }
        }

        protected override void OnSessionChange(SessionChangeEvent e)
        {
            SessionChangeDescription desc = e.Description;

            ITerminalServicesSession session = wtsServer.GetSession(desc.SessionId);
            ITerminalServicesSession consoleSession = wtsServer.GetConsoleSession();

            string clientUser = session.GetClientUser();

            if (Context.DebugLog)
            {
                var sb = new StringBuilder();
                sb.Append($"SessionChange: SessionId={desc.SessionId}, Reason={desc.Reason}");

                sb.Append("(");
                if (clientUser.Length > 0)
                    sb.Append($"User={clientUser}, ");
                sb.Append($"State={session.ConnectionState}");
                sb.Append(")");

                EventLog.WriteEntry(sb.ToString(), EventLogEntryType.Information, 91);
            }

            if (session.SessionId == consoleSession.SessionId)
                if (desc.Reason == SessionChangeReason.SessionLock)
                    ConsoleLocked = true;
                else if (desc.Reason == SessionChangeReason.SessionUnlock)
                    ConsoleLocked = false;

            if (desc.Reason == SessionChangeReason.SessionLogon
                || desc.Reason == SessionChangeReason.SessionUnlock && !session.IsRemoteConnected()
                || desc.Reason == SessionChangeReason.ConsoleConnect
                || desc.Reason == SessionChangeReason.RemoteConnect)
                if (session.ConnectionState == ConnectionState.Active)
                {
                    if (Context.EventLog)
                        EventLog.WriteEntry($"User login: {clientUser}", EventLogEntryType.Information, 1);

                    UserLogin?.Invoke(this, new UserLoginEventArgs { Session = session });
                }
        }
    }

    class UserLoginEventArgs : EventArgs
    {
        public ITerminalServicesSession Session { get; internal set; }
    }
}