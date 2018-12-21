using MadWizard.Insomnia.Exceptions;
using MadWizard.Insomnia.Modules.S3Handlers;
using MadWizard.Insomnia.Modules.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MadWizard.Insomnia.Modules.Session.NotifyAreaMessage;
using static MadWizard.Insomnia.Modules.Session.SessionHelperConfig;

namespace MadWizard.Insomnia.Modules.UI
{
    class TrayMenuController : ModuleBase
    {
        private bool controlTerminator = false;

        internal TrayMenuController()
        {
            AddDependency(typeof(SessionHelper));
        }

        #region EventHandler
        protected override void OnActivate(ServiceContext ctx)
        {
            Component<SessionHelper>().AutoStart = true;
            Component<SessionHelper>().Started += ConfigureSessionHelper;
        }
        protected override void OnStart()
        {
            try
            {
                Component<S3Terminator>().NetworkChanged += S3Terminator_NetworkChanged;
                Component<S3Terminator>().OptionChanged += S3Terminator_OptionsChanged;
                Component<S3Terminator>().HostsChanged += S3Terminator_OptionsChanged;

                controlTerminator = true;

                UpdateNotifyArea();
            }
            catch (ComponentNotFoundException)
            {
                controlTerminator = false;
            }
        }
        #endregion

        private void UpdateNotifyArea()
        {
            foreach (SessionHelperInstance instance in Component<SessionHelper>().Instances)
                UpdateSessionHelperConfig(instance);
        }
        private void SendNotifyMessage(NotifyAreaMessage message)
        {
            foreach (SessionHelperInstance instance in Component<SessionHelper>().Instances)
                instance.SendMessage(message);
        }

        #region S3Terminator
        private void S3Terminator_NetworkChanged(object sender, EventArgs e)
        {
            bool connected = Component<S3Terminator>().IsNetworkConnected;

            if (!connected)
            {
                SendNotifyMessage(new NotifyAreaMessage(NotifyMessageType.Warning, "Insomnia", $"Verbindung zu Netzwerk unterbrochen."));

                Thread.Sleep(500);
            }

            UpdateNotifyArea();

            if (connected)
                SendNotifyMessage(new NotifyAreaMessage(NotifyMessageType.None, "Insomnia", $"Verbindung zu Netzwerk wiederhergestellt."));
        }
        private void S3Terminator_OptionsChanged(object sender, EventArgs e)
        {
            UpdateNotifyArea();
        }
        #endregion

        #region SessionHelper
        private void ConfigureSessionHelper(SessionHelperInstance instance)
        {
            instance.MessageArrived += SessionHelperMessageArrived;

            UpdateSessionHelperConfig(instance);
        }
        private void UpdateSessionHelperConfig(SessionHelperInstance instance)
        {
            SessionHelperConfig config = instance.Config;

            config.DisplayNotifyIcon = true;

            if (controlTerminator)
            {
                bool displayWakeHosts = Component<S3Terminator>().IsNetworkConnected && Component<S3Terminator>().Hosts.Count > 0;

                config.DisplayNotifyIcon = displayWakeHosts;

                if (displayWakeHosts)
                {
                    SessionHelperWakeOptions options = new SessionHelperWakeOptions();
                    options.ResolveIP = Component<S3Terminator>().IsResolveIPAddress;
                    options.WakeHosts = Component<S3Terminator>().Hosts.Values.ToArray();
                    config.WakeOptions = options;
                }
                else
                    config.WakeOptions = null;
            }

            instance.Config = config;
        }
        private void SessionHelperMessageArrived(SessionHelperInstance instance, Message message)
        {
            if (message is ConfigureWakeHostMessage)
            {
                ConfigureWakeHostMessage whMessage = (ConfigureWakeHostMessage)message;

                Component<S3Terminator>().ConfigureWake(whMessage.Name, whMessage.Wake);
            }
            else if (message is ConfigureWakeOptionsMessage)
            {
                ConfigureWakeOptionsMessage woMessage = (ConfigureWakeOptionsMessage)message;

                Component<S3Terminator>().IsResolveIPAddress = woMessage.ResolveIP;
            }
        }
        #endregion
    }
}
