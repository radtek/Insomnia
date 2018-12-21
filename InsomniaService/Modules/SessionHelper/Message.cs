using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Session
{
    [Serializable]
    public abstract class Message
    {
        public static readonly string PIPE_NAME = "InsomniaPipe";

        protected Message()
        {

        }
    }

    [Serializable]
    public class InitializeMessage : ConfigMessage
    {
        internal InitializeMessage(SessionHelperConfig config) : base(config)
        {

        }
    }

    [Serializable]
    public class StartupMessage : Message
    {
        public StartupMessage(int pid)
        {
            PID = pid;
        }

        public int PID { get; private set; }
    }

    [Serializable]
    public class ConfigMessage : Message
    {
        internal ConfigMessage(SessionHelperConfig config)
        {
            Config = config;
        }

        public SessionHelperConfig Config { get; set; }
    }

    [Serializable]
    public class TextMessage : Message
    {
        internal TextMessage(string text, string caption = null)
        {
            Text = text;
            Caption = caption;
        }

        public string Caption { get; set; }
        public string Text { get; set; }
    }

    [Serializable]
    public class IdleTimeMessage : Message
    {
        public IdleTimeMessage(long time)
        {
            Time = time;
        }

        public long Time { get; private set; }
    }

    [Serializable]
    public class ConfigureWakeHostMessage : Message
    {
        public ConfigureWakeHostMessage(string name, bool wake)
        {
            Name = name;
            Wake = wake;
        }

        public string Name { get; private set; }
        public bool Wake { get; private set; }
    }

    [Serializable]
    public class ConfigureWakeOptionsMessage : Message
    {
        public bool ResolveIP { get; set; }
    }

    [Serializable]
    public class NotifyAreaMessage : Message
    {
        public const int DEFAULT_TIMEOUT = 10000;

        public NotifyAreaMessage(string title, string text) : this(NotifyMessageType.None, title, text)
        {

        }

        public NotifyAreaMessage(NotifyMessageType type, string title, string text, int timeout = DEFAULT_TIMEOUT)
        {
            Type = type;

            Title = title;
            Text = text;

            Timeout = timeout;
        }

        public NotifyMessageType Type { get; private set; }

        public string Title { get; private set; }
        public string Text { get; private set; }

        public int Timeout { get; private set; }

        [Serializable]
        public enum NotifyMessageType
        {
            None,
            Info,
            Warning,
            Error
        }
    }

    [Serializable]
    public class TerminateMessage : Message
    {
        internal TerminateMessage()
        {

        }
    }
}