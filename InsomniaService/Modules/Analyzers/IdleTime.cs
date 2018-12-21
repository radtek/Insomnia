using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MadWizard.Insomnia.Modules.Session;
using MadWizard.Insomnia.Tools;

namespace MadWizard.Insomnia.Modules.Analyzers
{
    class IdleTime : ModuleBase
    {
        IDictionary<int, User> users = new Dictionary<int, User>();

        internal IdleTime()
        {
            AddDependency(typeof(IdleScanner));
            AddDependency(typeof(SessionHelper));
            AddDependency(typeof(TerminalServer));
        }

        protected override void OnActivate(ServiceContext ctx)
        {
            Component<SessionHelper>().AutoStart = true;
            Component<SessionHelper>().Started += SessionHelper_Connected;
            Component<SessionHelper>().Terminated += SessionHelper_Disconnected;

            Component<IdleScanner>().Scan += QueryIdleTimers;
        }

        private void QueryIdleTimers(object sender, IdleScanAnalysis analysis)
        {
            foreach (User user in users.Values)
            {
                if (user.IdleTime >= 0 && user.IdleTime < Context.Interval)
                {
                    // Ist der Benutzer NICHT bereits über RDP angemeldet?
                    if (!Component<TerminalServer>()[user.SID].IsRemoteConnected())
                        analysis.BusyTokens.Add("<" + user.Name + ">");

                    analysis.Busy = true;
                }
            }
        }

        #region SessionHelper
        private void SessionHelper_Connected(SessionHelperInstance helper)
        {
            int sid = helper.SID;
            string name = Component<TerminalServer>()[sid].UserName;
            users.Add(sid, new User(sid, name));

            helper.MessageArrived += SessionHelper_MessageArrived;

            SessionHelperConfig config = helper.Config;
            config.MonitorIdleTime = true;
            helper.Config = config;
        }
        private void SessionHelper_MessageArrived(SessionHelperInstance helper, Message message)
        {
            if (message is IdleTimeMessage)
            {
                users[helper.SID].IdleTime = (message as IdleTimeMessage).Time;
            }
        }
        private void SessionHelper_Disconnected(SessionHelperInstance helper)
        {
            helper.MessageArrived -= SessionHelper_MessageArrived;

            users.Remove(helper.SID);
        }
        #endregion

        private class User
        {
            internal User(int sid, string name)
            {
                SID = sid;
                Name = name;

                IdleTime = -1;
            }

            public int SID { get; private set; }
            public string Name { get; private set; }

            public long IdleTime { get; internal set; }
        }
    }
}