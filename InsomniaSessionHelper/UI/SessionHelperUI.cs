using MadWizard.Insomnia.Modules.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MadWizard.Insomnia.UI
{
    class SessionHelperUI
    {
        internal readonly SessionHelper helper;

        SessionHelperTrayMenu trayMenu;

        SynchronizationContext uiContext;

        ManualResetEvent wait;

        internal SessionHelperUI(SessionHelper helper)
        {
            this.helper = helper;

            trayMenu = new SessionHelperTrayMenu(this);

            helper.IncomingMessage += SessionHelper_IncomingMessage;
            helper.Termination += SessionHelper_Termination;
        }

        #region Message Loop
        internal void Start()
        {
            using (wait = new ManualResetEvent(false))
            {
                Thread thread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Idle += FinishStartup;
                    Application.Run();
                });

                thread.Name = GetType().Name;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                wait.WaitOne();
            }
        }

        private void FinishStartup(object sender, EventArgs e)
        {
            Application.Idle -= FinishStartup;

            uiContext = SynchronizationContext.Current;

            wait.Set();
        }

        protected internal void InvokeAction(Action action)
        {
            if (uiContext == null)
                throw new InvalidOperationException();

            uiContext.Send(new SendOrPostCallback((obj) => action()), null);
        }

        internal void Stop()
        {
            InvokeAction(() =>
            {
                Application.ExitThread();

                uiContext = null;
            });
        }
        #endregion

        #region SessionHelper
        private void SessionHelper_IncomingMessage(object sender, MessageEvent e)
        {
            if (e.Message is TextMessage)
            {
                TextMessage msg = (TextMessage)e.Message;

                MessageBox.Show(msg.Text, msg.Caption);
            }
        }
        private void SessionHelper_Termination(object sender, EventArgs e)
        {
            Stop();
        }
        #endregion

    }
}