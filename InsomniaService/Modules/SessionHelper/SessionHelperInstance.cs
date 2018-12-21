using MadWizard.Insomnia.Tools;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Session
{
    class SessionHelperInstance
    {
        internal NamedPipeConnection<Message, Message> pipe;

        private SessionHelperConfig? config;

        internal bool forcedKill = false;

        internal SessionHelperInstance(NamedPipeConnection<Message, Message> connection)
        {
            pipe = connection;
            pipe.ReceiveMessage += Pipe_ReceiveMessage;
        }

        public int PID => Process.Id;
        public int SID => Process.SessionId;

        public Process Process { get; private set; }

        public SessionHelperConfig Config
        {
            get
            {
                return config.Value;
            }

            set
            {
                if (config == null)
                    SendMessage(new InitializeMessage(value));
                else
                    SendMessage(new ConfigMessage(value));

                config = value;
            }
        }

        public event SessionHelperEventHandler Started;
        public event MessageEventHandler MessageArrived;
        public event TerminationEventHandler Terminated;

        #region Pipe & Process
        private void Pipe_ReceiveMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if (message is StartupMessage)
            {
                if (Process != null)
                    throw new InvalidOperationException();

                Process = Process.GetProcessById(((StartupMessage)message).PID);
                Process.EnableRaisingEvents = true;
                Process.Exited += Process_Exited;

                Started?.Invoke(this);
            }
            else
            {
                MessageArrived?.Invoke(this, message);
            }
        }
        private void Process_Exited(object sender, EventArgs e)
        {
            Terminated?.Invoke(this, forcedKill);
        }
        #endregion

        public void SendMessage(Message message)
        {
            pipe.PushMessage(message);
        }

        public void Terminate(double? timeout = null, bool wait = false)
        {
            using (var waiter = new ManualResetEvent(false))
            {
                if (wait)
                {
                    Terminated += (s, f) =>
                    {
                        waiter.Set();
                    };
                }

                if (timeout != null)
                {
                    var timer = new System.Timers.Timer(timeout.Value);
                    timer.AutoReset = false;
                    timer.Elapsed += (t, e) =>
                    {
                        if (!Process.HasExited)
                        {
                            forcedKill = true;

                            Process.Kill();
                        }
                    };

                    timer.Start();
                }

                SendMessage(new TerminateMessage());

                if (wait)
                {
                    waiter.WaitOne();
                }
            }
        }

        public void Kill()
        {
            Process.Kill();
        }

        internal delegate void MessageEventHandler(SessionHelperInstance session, Message message);
        internal delegate void TerminationEventHandler(SessionHelperInstance session, bool forced);
    }
}