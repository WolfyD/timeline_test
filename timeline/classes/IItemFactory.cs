using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace timeline.classes
{
    public interface IItemFactory
    {
        IItem Create(string name);
    }
}
