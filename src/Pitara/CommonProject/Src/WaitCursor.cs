using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CommonProject.Src
{
    public class WaitCursor
    {
        public WaitCursor()
        {
            Mouse.OverrideCursor = Cursors.Wait;
        }
        public void Stop()
        {
            Mouse.OverrideCursor = null;
        }
    }
}
