using MadWizard.Insomnia.Modules.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Modules.Test
{
    class TestSessionHelper : ModuleBase
    {
        internal TestSessionHelper()
        {
            AddDependency(typeof(SessionHelper));
        }

        protected override void OnStart()
        {
            var helper = Component<SessionHelper>().LaunchHelper();
            helper.SendMessage(new TextMessage("Hallo Welt!", "Test"));
            helper.Terminate();
        }
    }
}