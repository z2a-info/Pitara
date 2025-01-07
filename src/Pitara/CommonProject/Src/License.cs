// using Pitara.PhotoStuff;
using CommonProject.Src.Cache;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text;
// using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class License
    {
        private LicenseCache _cache;
        private OperatingSettings _operatingSettings;
        private ILogger _logger;
        public string LicenseCode { get; set; } = string.Empty;
        public string ContextCode { get; set; } = string.Empty;

        [JsonIgnore]
        public bool LicensedVersion { get; set; } = true;

        public License(LicenseCache cache, OperatingSettings operatingSettings, ILogger logger)
        {
            _cache = cache;
            _operatingSettings = operatingSettings;
            _logger = logger;
        }
        public void PromptForRegistrationIfNecessary()
        {
            // There is a license file and its validated.
            if (ValidateLicense())
            {
                _logger.SendLogAsync("License validated.");
                return;
            }

            // Don't show usage days warning on first day.
            DateTime today = DateTime.Now;
            TimeSpan span = today - _operatingSettings.StartingDate;
            if (span.Days == 0)
            {
                _logger.SendLogAsync("First day, so not prompting for registration.");
            }
            if (span.Days >= 1) // change it so 1 instead of 0s
            {
                if (span.Days < 14)
                {
                    DateTime enddate = _operatingSettings.StartingDate.AddDays(15);
                    TimeSpan timeSpan = enddate - today;
                    var daysRemaining = timeSpan.Days;

                    Utils.DisplayMessageBox($"Thank you for using Pitara.\nYour trial period will end in: {daysRemaining} days on: {enddate.ToString("MM-dd-yyyy")}.\n\nWe will appreciate if you purchase a license to support the Pitara team. Please click license menu then click purchase license to get one.");
                    // Full featured until trial period ends.
                    LicensedVersion = true;
                }
                else
                {
                    Utils.DisplayMessageBox($"Thank you for using Pitara. Your trial period is ended. But you can continue to use Pitara with limited features.\n\nWe will appreciate if you purchase a license. It supports ongoing effort with bug fixes and improvements.");
                    LicensedVersion = false;
                }
            }
        }
        private bool ValidateLicense()
        {
            if (string.IsNullOrEmpty(ContextCode) || string.IsNullOrEmpty(LicenseCode))
            {
                _logger.SendLogAsync("No license discovered.");
                // throw new Exception("ContextCode & LicenseCode must not be empty");
                return false;
            }
            var contextCode = GenerateContext();
            if (contextCode.Equals(ContextCode))
            {
                return true;
            }
            else
            {
                _logger.SendLogAsync("License was tempered.");
                Utils.DisplayMessageBox("License is tempered or doesn't belong to this machine.");
                return false;
            }
        }
        public async Task<bool> ActivateAsync()
        {
            ContextCode = GenerateContext();
            var postObject = JsonConvert.SerializeObject(this);

            using (var client = new HttpClient())
            {
                var response = await client.PostAsync(
                    "https://GetPitara.com/license-server/activate.php",
                     new StringContent(postObject, Encoding.UTF8, "application/json"));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var contents = await response.Content.ReadAsStringAsync();
                    Utils.DisplayMessageBox("Your license code is registered!\nEnjoy full functionalities of Pitara. As always feel free to reach out for any comments/questions.");
                    LicensedVersion = true;
                    return true;
                }
                Utils.DisplayMessageBox($"License couldn't be activated.\n Error:{response.ReasonPhrase}");
            }
            LicensedVersion = false;
            return false;
        }
        public static string GenerateUniqueDeviceId()
        {
            string contectMaterial = string.Empty;

            var mbs = new ManagementObjectSearcher("Select ProcessorId From Win32_processor");
            ManagementObjectCollection mbsList = mbs.Get();
            string id = "";
            foreach (ManagementObject mo in mbsList)
            {
                id = mo["ProcessorId"].ToString();
                break;
            }
            contectMaterial += id;
            byte[] hmacHash = PhotoManipulation.HMACSHA256Hash(contectMaterial, "485bce6a-80b5-46d7-b5c0-02669cac608d");
            return PhotoManipulation.ByteToHash(hmacHash);
        }
        private string GenerateContext()
        {
            string contectMaterial = string.Empty;
            
            var mbs = new ManagementObjectSearcher("Select ProcessorId From Win32_processor");
            ManagementObjectCollection mbsList = mbs.Get();
            string id = "";
            foreach (ManagementObject mo in mbsList)
            {
                id = mo["ProcessorId"].ToString();
                break;
            }
            contectMaterial += id;
            byte[] hmacHash = PhotoManipulation.HMACSHA256Hash(contectMaterial, "Foo"+LicenseCode+"Bar");
            return PhotoManipulation.ByteToHash(hmacHash);
        }
        public async Task ReadLicenseAsync()
        {
            await _cache.LoadAsync();
            if (_cache.DataKeyPairDictionary.ContainsKey("LicenseDetails"))
            {
                var license = _cache.DataKeyPairDictionary["LicenseDetails"];
                //  Decrypt and set it.
                LicenseCode = license.LicenseCode;
                ContextCode = license.ContextCode;
            }
        }
        public async Task WriteLicenseAsync()
        {
            // Encrypt.

            _cache.Add("LicenseDetails", this);
            await _cache.SaveAsync();
        }
    }
}
