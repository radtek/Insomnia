using MadWizard.Insomnia.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    abstract class ModuleBase
    {
        private InsomniaService service;

        private List<Type> dependencies;

        protected ModuleBase()
        {
            dependencies = new List<Type>();
        }

        protected internal InsomniaService Service
        {
            get
            {
                return service;
            }

            internal set
            {
                if (service != null)
                    throw new InvalidOperationException();

                service = value;

                service.ModuleActivation += (s, e) => OnActivate(e.Context);
                service.ModuleEvent += (s, e) => OnEvent((ModuleBase)s, e);

                service.Startup += (s, e) => OnStart();
                service.PowerChange += (s, e) => OnPowerChange(e);
                service.SessionChange += (s, e) => OnSessionChange(e);
                service.Shutdown += (s, e) => OnShutdown();
            }
        }

        public Type[] Dependencies => dependencies.ToArray();

        protected ServiceContext Context => service.ctx;
        protected EventLog EventLog => service.EventLog;

        protected void AddDependency(Type type)
        {
            if (!type.IsSubclassOf(typeof(ModuleBase)))
                throw new ArgumentException();
            if (dependencies.Contains(type))
                throw new ArgumentException();
            dependencies.Add(type);
        }

        protected void PublishEvent(EventBase e)
        {
            service.SendEvent(this, e);
        }

        protected T Component<T>() where T : ModuleBase
        {
            T comp = (T)service[typeof(T)];
            //if (comp == null)
            //    throw new ComponentNotFoundException(typeof(T));
            return comp;
        }

        protected virtual void OnActivate(ServiceContext ctx)
        {

        }

        protected virtual void OnStart()
        {

        }

        protected virtual void OnEvent(ModuleBase source, EventBase e)
        {

        }

        protected virtual void OnPowerChange(PowerChangeEvent e)
        {
            switch (e.Status)
            {
                case PowerBroadcastStatus.Suspend:
                    OnSuspend();
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    OnResumeSuspend();
                    break;
            }
        }
        protected virtual void OnSuspend()
        {

        }
        protected virtual void OnResumeSuspend()
        {

        }
        protected virtual void OnSessionChange(SessionChangeEvent e)
        {

        }

        protected virtual void OnShutdown()
        {

        }
    }
}