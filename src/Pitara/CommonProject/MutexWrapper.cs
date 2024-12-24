using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject
{
    public class MutexWrapper: IDisposable
    {
        Mutex _mutex = null;    
        public MutexWrapper(string name) 
        {
            _mutex = new Mutex(false, name);
            _mutex.WaitOne(Timeout.Infinite, false);
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
