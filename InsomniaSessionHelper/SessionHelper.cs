using System.Threading;
using System.IO;
using MadWizard.Insomnia.Modules.Session;

using NamedPipeWrapper;
using System.Diagnostics;
using System;
using System.Reflection;
using MadWizard.Insomnia.UI;

namespace MadWizard.Insomnia
{
    class SessionHelper
    {
        const string CMD_STARTUP_DELAY = "-StartupDelay=";
        const string CMD_DEBUG_LOG = "-DebugLog";
        const string CMD_HEADLESS = "-headless";

        internal readonly string NAME;

        NamedPipeClient<Message> pipeClient;

        SessionHelperConfig config;

        SessionMonitor monitor;

        SessionHelperUI ui;

        static void Main(string[] args)
        {
            bool headless = false;
            foreach (string arg in args)
            {
                if (arg.StartsWith(CMD_STARTUP_DELAY))
                {
                    int startupDelay = int.Parse(arg.Replace(CMD_STARTUP_DELAY, ""));

                    Thread.Sleep(startupDelay);
                }
                else if (arg.StartsWith(CMD_DEBUG_LOG))
                {
                    Debug.Listeners.Add(new TraceListener(new FileInfo("helper.log")));
                }
                else if (arg.StartsWith(CMD_HEADLESS, StringComparison.InvariantCultureIgnoreCase))
                {
                    headless = true;
                }
            }

            var helper = new SessionHelper(headless);

            Debug.WriteLine("Launched with parameters: " + string.Join(" ", args), helper.NAME);

            helper.Start().Wait().Terminate();
        }

        private SessionHelper(bool headless)
        {
            NAME = $"SessionHelper[{Process.GetCurrentProcess().SessionId}]";

            pipeClient = new NamedPipeClient<Message>(Message.PIPE_NAME);
            pipeClient.ServerMessage += PipeClient_ServerMessage;
            pipeClient.Disconnected += PipeClient_Disconnected;
            pipeClient.Error += PipeClient_Error;

            monitor = new SessionMonitor(this);

            if (!headless)
            {
                ui = new SessionHelperUI(this);
            }
        }

        internal SessionHelperConfig Config
        {
            get
            {
                return config;
            }

            set
            {
                ConfigChanged?.Invoke(this, new ConfigChangedEvent { Config = (config = value) });
            }
        }

        internal event EventHandler<MessageEvent> IncomingMessage;
        internal event EventHandler<ConfigChangedEvent> ConfigChanged;
        internal event EventHandler<EventArgs> Termination;

        internal void SendMessage(Message message)
        {
            Debug.WriteLine("Outgoing message -> " + message.GetType().Name, NAME);

            pipeClient.PushMessage(message);
        }

        #region Helper Control
        SessionHelper Start()
        {
            ui?.Start();

            pipeClient.Start();

            Debug.WriteLine($"NAMED_PIPE['{Message.PIPE_NAME}'] client started", NAME);

            return this;
        }
        SessionHelper Wait()
        {
            pipeClient.WaitForDisconnection();

            return this;
        }

        void Terminate()
        {
            Debug.WriteLine("Terminating...", NAME);

            Termination?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region PipeClient
        void PipeClient_ServerMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            Debug.WriteLine("Incoming message <- " + message.GetType().Name, NAME);

            if (message is ConfigMessage)
                HandleMessage((ConfigMessage)message);
            else if (message is TerminateMessage)
                HandleMessage((TerminateMessage)message);
            else
                IncomingMessage?.Invoke(this, new MessageEvent { Message = message });
        }
        private void PipeClient_Disconnected(NamedPipeConnection<Message, Message> connection)
        {
            Debug.WriteLine("Disconnected", NAME);
        }
        private void PipeClient_Error(Exception exception)
        {
            Debug.Fail("PipeClient Error", exception.ToString());
        }
        #endregion

        #region Message Handlers
        void HandleMessage(ConfigMessage message)
        {
            Config = message.Config;

            if (message is InitializeMessage)
            {
                SendMessage(new StartupMessage(Process.GetCurrentProcess().Id));
            }
        }
        void HandleMessage(TerminateMessage message)
        {
            pipeClient.Stop();
        }
        #endregion
    }

    class MessageEvent : EventArgs
    {
        public Message Message { get; set; }
    }

    class ConfigChangedEvent : EventArgs
    {
        public SessionHelperConfig Config { get; set; }
    }
}