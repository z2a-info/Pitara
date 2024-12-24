using CommonProject.Src.Cache;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CommonProject.Src.Queues
{
    public class MetaQueueMessage
    {
        public RecepientType Recepient { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
    }
    public class MetaQueue<T>: BaseQueue<T>
    {
        public static ConcurrentQueue<T> _theQueue = new ConcurrentQueue<T>();
        private ILogger _logger;
        private ILuceneService _luceneService;
        private AppSettings _appSettings;


        public MetaQueue(int timerFrequency, int maxBatchSize, ILuceneService luceneService, AppSettings appSettings, ManualResetEvent searchisGoingon, ILogger logger)
            : base(_theQueue, timerFrequency, maxBatchSize, KnobId.MetaQueueMessage, searchisGoingon ,logger)
        {
            _logger = logger;
            _luceneService = luceneService;
            _appSettings = appSettings;
        }
        public static async Task EnQueueAsync(T item)
        {
            await Task.Run(() => _theQueue.Enqueue(item));
        }

        public override async Task ProcessBatch(IList<T> batch)
        {
            try
            {
                LocationCache gpsCache = new LocationCache(_appSettings.GpsDBFileName, _logger, _appSettings);
                await gpsCache.LoadAsync();

                TimeCache dbTime = new TimeCache(_appSettings.TimeDBFileName, _logger, _appSettings);
                await dbTime.LoadAsync();

                KeyValueCache dbCust = new KeyValueCache(
                    _appSettings.CustomKeywordsDBFileName, _logger, _appSettings);
                await dbCust.LoadAsync();

                KeyValueCache dbFileFolder = new KeyValueCache(
                    _appSettings.FileFolderKeywordsDBFileName, _logger, _appSettings);
                await dbFileFolder.LoadAsync();

                KeyValueCache dbHeight = new KeyValueCache(
                    _appSettings.HeightKeywordsDBFileName, _logger, _appSettings);
                await dbHeight.LoadAsync();

                KeyValueCache cameraModelMake = new KeyValueCache(
                    _appSettings.CameraModelMakeKeywordsFileName, _logger, _appSettings);
                await cameraModelMake.LoadAsync();

                KeyValueCache misc = new KeyValueCache(
                    _appSettings.MiscTagsFileName, _logger, _appSettings);
                await misc.LoadAsync();
                
                foreach (var item in batch)
                {
                    var message = item as MetaQueueMessage;
                    if (string.IsNullOrEmpty(message.Message.Trim()))
                    {
                        continue;
                    }
                    switch (message.Recepient)
                    {
                        case RecepientType.GPS_DATA:
                            {
                                string[] parts = message.Message.Split(new char[] { ':' });
                                gpsCache.Add(parts[0].Trim(), parts[1].Trim());
                                break;
                            }
                        case RecepientType.TIME_DATA:
                            {
                                string[] parts = message.Message.Split(new char[] { ':' });
                                dbTime.Add(parts[0].Trim(), parts[1].Trim());
                                break;
                            }
                        case RecepientType.CUSTOMEKEYWORD_DATA:
                            {
                                await dbCust.Add(message.Message);
                                break;
                            }
                        case RecepientType.HEIGHT_DATA:
                            {
                                await dbHeight.Add(message.Message);
                                break;
                            }
                        case RecepientType.FILE_FOLDER_DATA:
                            {
                                await dbFileFolder.Add(message.Message);
                                break;
                            }
                        case RecepientType.CAMERA_MODEL_MAKE_TAGS:
                            {
                                await cameraModelMake.Add(message.Message, false);
                                break;
                            }
                        case RecepientType.MISC_DATA:
                            {
                                await misc.Add(message.Message);
                                break;
                            }
                        default:
                            {
                                _logger.SendLogAsync($"Undefined RecepientType: {message.Recepient}");
                                break;
                            }
                    }
                } // for each
                await dbTime.SaveAsync();
                await dbCust.SaveAsync();
                await gpsCache.SaveAsync();
                await dbFileFolder.SaveAsync();
                await dbHeight.SaveAsync();
                await cameraModelMake.SaveAsync();
                await misc.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.SendLogWithException($"Error processing Meta queue", ex);
                throw;
            }
        }
    }
}
