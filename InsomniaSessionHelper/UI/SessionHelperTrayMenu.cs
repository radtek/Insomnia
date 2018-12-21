using MadWizard.Insomnia.Modules.S3Handlers;
using MadWizard.Insomnia.Modules.Session;
using MadWizard.Insomnia.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MadWizard.Insomnia.Modules.Session.SessionHelperConfig;

namespace MadWizard.Insomnia.UI
{
    class SessionHelperTrayMenu
    {
        SessionHelperUI UI;

        SessionHelperWakeOptions? wakeOptions;

        private NotifyIcon notifyIcon;

        internal SessionHelperTrayMenu(SessionHelperUI ui)
        {
            UI = ui;
            UI.helper.IncomingMessage += SessionHelper_IncomingMessage;
            UI.helper.ConfigChanged += SessionHelper_ConfigChanged;
            UI.helper.Termination += SessionHelper_Termination;
        }

        #region SessionHelper
        private void SessionHelper_IncomingMessage(object sender, MessageEvent e)
        {
            if (e.Message is NotifyAreaMessage)
            {
                NotifyAreaMessage msg = (NotifyAreaMessage)e.Message;

                UI.InvokeAction(() => notifyIcon.ShowBalloonTip(msg.Timeout, msg.Title, msg.Text, (ToolTipIcon)msg.Type));
            }
        }
        private void SessionHelper_ConfigChanged(object sender, ConfigChangedEvent e)
        {
            wakeOptions = e.Config.WakeOptions;

            UI.InvokeAction(() =>
            {
                if (e.Config.DisplayNotifyIcon)
                {
                    if (notifyIcon == null)
                        CreateNotifyArea();

                    UpdateContextMenu();
                }
                else if (notifyIcon != null)
                    DestroyNotifyArea();
            });
        }
        private void SessionHelper_Termination(object sender, EventArgs e)
        {
            UI.InvokeAction(() => DestroyNotifyArea());
        }
        #endregion

        #region NotifyArea
        private void CreateNotifyArea()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Text = "Insomnia";
            notifyIcon.Icon = Resources.MoonWhiteOutline12;
            notifyIcon.Visible = true;
        }
        private void UpdateContextMenu()
        {
            notifyIcon.ContextMenu?.Dispose();

            notifyIcon.ContextMenu = new ContextMenu();

            if (wakeOptions != null)
            {
                MenuItem header = new MenuItem("[Wake On LAN]");
                header.Enabled = false;
                notifyIcon.ContextMenu.MenuItems.Add(header);

                notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator

                foreach (WakeHost host in wakeOptions.Value.WakeHosts)
                {
                    Debug.WriteLine($"UI :: Display {host}", UI.helper.NAME);

                    MenuItem item = new MenuItem(host.Name);
                    item.Tag = host;
                    item.Checked = host.IsWaking;
                    item.Click += ContextMenuHostClicked;
                    notifyIcon.ContextMenu.MenuItems.Add(item);
                }

                if (wakeOptions.Value.WakeHosts.Count() > 0)
                    notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator

                MenuItem menuOptions = new MenuItem("Optionen");
                {
                    MenuItem optionResolveIP = new MenuItem("IP-Adresse auflösen");
                    optionResolveIP.Checked = wakeOptions.Value.ResolveIP;
                    optionResolveIP.Click += ContextMenuOptionsResolveIPClicked;
                    menuOptions.MenuItems.Add(optionResolveIP);
                }

                notifyIcon.ContextMenu.MenuItems.Add(menuOptions);
            }
        }
        private void ContextMenuHostClicked(object sender, EventArgs e)
        {
            WakeHost host = (WakeHost)(sender as MenuItem).Tag;

            UI.helper.SendMessage(new ConfigureWakeHostMessage(host.Name, !host.IsWaking));
        }
        private void ContextMenuOptionsResolveIPClicked(object sender, EventArgs e)
        {
            bool resolveIP = !(sender as MenuItem).Checked;

            UI.helper.SendMessage(new ConfigureWakeOptionsMessage { ResolveIP = resolveIP });
        }
        private void DestroyNotifyArea()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }
        }
        #endregion
    }
}