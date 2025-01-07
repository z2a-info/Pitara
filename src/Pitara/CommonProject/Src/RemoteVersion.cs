using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class RemoteVersion
    {
        // private static ILogger _logger = AsyncLog.GetGlobalLogger();

        public async Task<Version> CheckCurrentVersion(ILogger logger)
        {
            try
            {
                // return new Version("0.0.0.0");
                HttpClient client = new HttpClient();
                HttpResponseMessage result = await client.GetAsync(new Uri("https://getpitara.com/en/download-pitara/build/PitaraVersion.txt"));
                if (!result.IsSuccessStatusCode)
                {
                    return new Version();
                }
                var content = await result.Content.ReadAsStringAsync();
                string[] parts = content.Split(new char[] { '@' });
                return new Version(parts[0]);
            }
            catch (Exception ex)
            {
                logger.SendLogAsync($"Couldn't check current released version. Erroe: {ex.Message}");
                return new Version("0.0.0.0");
            }
        }
        public async Task<Version> CheckCurrentTestVersion(ILogger logger)
        {
            try
            {
                // return new Version("0.0.0.0");
                HttpClient client = new HttpClient();
                HttpResponseMessage result = await client.GetAsync(new Uri("https://getpitara.com/setup/test/PitaraVersion.txt"));
                if (!result.IsSuccessStatusCode)
                {
                    return new Version();
                }
                var content = await result.Content.ReadAsStringAsync();
                string[] parts = content.Split(new char[] { '@' });
                return new Version(parts[0]);
            }
            catch (Exception ex)
            {
                logger.SendLogAsync($"Couldn't check current released version. Erroe: {ex.Message}");
                return new Version("0.0.0.0");
            }
        }
    }
}