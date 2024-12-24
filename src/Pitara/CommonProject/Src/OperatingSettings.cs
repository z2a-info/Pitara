using CommonProject.Src.Cache;
using System;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public class OperatingSettings
    {
        public OperatingSettings(BaseThreadSafeFileCache<OperatingSettings> cache)
        {
            _cache = cache;
        }
        public async Task LoadAsync()
        {
            await _cache.LoadAsync();
            if (_cache.DataKeyPairDictionary.ContainsKey("OperatingSettings"))
            {
                StartingDate = _cache.DataKeyPairDictionary["OperatingSettings"].StartingDate;
                ClientId = _cache.DataKeyPairDictionary["OperatingSettings"].ClientId;
                DoNotShowAddTagsWarning = _cache.DataKeyPairDictionary["OperatingSettings"].DoNotShowAddTagsWarning;
                AccountedFor = _cache.DataKeyPairDictionary["OperatingSettings"].AccountedFor;
                // Hash = _cache.DataKeyPairDictionary["OperatingSettings"].Hash;
                // var magicString = "dafdad9b-2fa0-4ac7-a703-7a3b96ed4860" + StartingDate + "a110bcfedd7d8b53139ecbc28a5e83c3";
            }
            else
            {
                //  Inite case
                StartingDate = DateTime.Now;
                ClientId = License.GenerateUniqueDeviceId();
                AccountedFor = false;
                _cache.AddCommon("OperatingSettings", this);
                
                await _cache.SaveAsync();
            }
        }
        public async Task SaveAsync()
        {
            _cache.DataKeyPairDictionary["OperatingSettings"].StartingDate = StartingDate;
            _cache.DataKeyPairDictionary["OperatingSettings"].ClientId = ClientId;
            _cache.DataKeyPairDictionary["OperatingSettings"].DoNotShowAddTagsWarning = DoNotShowAddTagsWarning;
            _cache.DataKeyPairDictionary["OperatingSettings"].AccountedFor = AccountedFor;
            await _cache.SaveAsync();
        }
        public bool DoNotShowAddTagsWarning { get; set; } = false;
        public DateTime StartingDate { get; set; }
        public string ClientId { get; set; }
        public bool AccountedFor { get; set; }
        // public string Hash{ get; set; }

        private BaseThreadSafeFileCache<OperatingSettings> _cache;
    }
}
