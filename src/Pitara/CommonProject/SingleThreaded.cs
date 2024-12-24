using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CommonProject
{
    public class SingleThreaded : IDisposable
    {
        Semaphore _semaphore = null;
        string _finalName = null;
        bool _locked = false;
        public SingleThreaded([CallerMemberName] string methodName = "", [CallerFilePath] string fileName = "")
        {
             _finalName = Path.GetFileNameWithoutExtension(fileName) + "." + methodName;
            _semaphore = new Semaphore(1, 1, _finalName);
        }
        public bool IsSafeToProceed()
        {
            try
            {
                if (_semaphore.WaitOne(2))
                {
                    _locked = true;
                    return true;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return false;
        }

        public void Dispose()
        {
            try
            {
                if(_locked)
                {
                    _semaphore.Release();
                }
                _semaphore.Dispose();
                _semaphore = null;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
