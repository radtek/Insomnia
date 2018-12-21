using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Exceptions
{
    internal class ComponentNotFoundException : Exception
    {
        internal ComponentNotFoundException(Type type) : base(type.Name)
        {

        }
    }
}
