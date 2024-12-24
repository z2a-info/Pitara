using System;
using System.Reflection;

namespace CommonProject.Src
{
    public class CurrentVersionWrapper
    {
        // Update index version when a new field is added to the index
        private static string _supportedIndexVersion { get; } = "10"; 
        private const string _currentBaseVersion = "2.0.";
        public const string CurrentAssemblyVersion = _currentBaseVersion + "*";
        public static Version GetVersion()   
        {
           //  string exeAssembly = Assembly.GetEntryAssembly().Location;
            return Assembly.GetEntryAssembly().GetName().Version;
            // return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;    
        }
        public static string GetSupportedIndexVersion()
        {
            return _supportedIndexVersion;
        }
    }
}
