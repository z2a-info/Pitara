using CommonProject.Src.Cache;
using CommonProject.Src.Queues;
using Lucene.Net.Documents;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace CommonProject.Src
{
    public static class PhotoManipulation
    {
        private static ILogger _logger;
        private static AppSettings _appSettings;
        public static void Init(ILogger logger, AppSettings appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;
        }
        public static async Task<List<Document>> GetDocumentsAsync(string[] photoPaths, string clientId)
        {
            List<Document> results = new List<Document>();
            List<string> badFiles = new List<string>();
            List<Task> taskArray = new List<Task>();
            Object lockObject = new object();
            foreach (var photoPath in photoPaths)
            {
                taskArray.Add(Task.Run(async () =>
                {
                    try
                    {
                        await Utils.DelayRandome(50, 200);
                        StringBuilder sb = new StringBuilder();
                        var photoMeta = await GetPhotoMetaAsync(photoPath);

                        // If there is no thumbnail data. Let's not insert into index.
                        // This way it has a chance to be added later on re-launch.
                        if (string.IsNullOrEmpty(photoMeta.ThumbNail))
                        {
                            lock (lockObject)
                            {
                                badFiles.Add(photoPath);
                            }
                            _logger.SendLogAsync($"Bad formatted file. Added to {Path.GetFileName(_appSettings.BadFilesName)}. File : {photoPath}");
                            return;
                        }

                        // Start early this is most time taking.
                        // var contentKeyTask = Task<string>.Run(() => HashOfPhotoAsync(photoPath));
                        var locationTagsTask = Task<string>.Run(() => GetSuggestedTagsFromGeoDataAsync(photoMeta.Longitude, photoMeta.Lattitude, clientId));
                        
                        var folderTags = GetSuggestedTagsFromFolderName(photoPath);
                        var dateTimeKeywords = await GetDateTimeTagsAsync(photoMeta.DateTaken);
                        sb.Append(dateTimeKeywords);
                        sb.Append(" ");
                        sb.Append(folderTags);
                        sb.Append(" ");
                        var location = await locationTagsTask;
                        sb.Append(location);
                        sb.Append(" ");
                        
                        var heightTag = GetHeightTag(photoMeta.Altitude);
                        if(!string.IsNullOrEmpty(heightTag))
                        {
                            await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                            {
                                Recepient = RecepientType.HEIGHT_DATA,
                                Message = heightTag 
                            });
                            sb.Append(heightTag);
                            sb.Append(" ");
                        }
                        sb.Append(
                            photoMeta.Comment 
                            + " " 
                            + photoMeta.CustomeKeyWords 
                            + " " 
                            + photoMeta.CameraModel
                            + " "
                            + photoMeta.CameraMake
                             );
                        var trimmedDownDateTime = RemoveRedundantKeywords(dateTimeKeywords);
                        string miscTags = photoMeta.Comment + " " + trimmedDownDateTime + " " + heightTag;
                        string allTags = TagsHelper.NormalizeTAGs(
                            sb.ToString().ToLower(), false, true, 1, true, true, -1).ToLower();

                        // Lets add Camera details without normalizing as well.
                        allTags += " " + photoMeta.CameraMake;
                        allTags += " " + photoMeta.CameraModel;
                        allTags = Utils.RemoveDuplicateWords(allTags);

                        await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                        {
                            Recepient = RecepientType.MISC_DATA,
                            Message = miscTags
                        });
                        await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                        {
                            Recepient = RecepientType.CAMERA_MODEL_MAKE_TAGS,
                            Message =
                            photoMeta.CameraMake
                            + " "
                            + photoMeta.CameraModel
                            //+ " "
                            //+ photoMeta.CameraMake
                        });
                        await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                        {
                            Recepient = RecepientType.CUSTOMEKEYWORD_DATA,
                            Message = photoMeta.CustomeKeyWords
                        });
                        await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                        {
                            Recepient = RecepientType.FILE_FOLDER_DATA,
                            Message = folderTags
                        });

                        Document doc  = CreatePhotoDocument(
                            photoPath,
                            allTags, 
                            photoMeta.CustomeKeyWords.Trim(),
                            location.Trim(),
                            dateTimeKeywords.Trim(),
                            photoMeta.ThumbNail
                            );

                        if (photoMeta.DateTaken.HasValue)
                        {
                            doc.Add(new NumericField("EPOCHTIME", Field.Store.YES, true)
                                .SetLongValue(long.Parse(PhotoManipulation.ToEpochTime(photoMeta.DateTaken.Value))));
                            // doc.AddCommon(new Field("EPOCHTIME", PhotoManipulation.ToEpochTime(photoMeta.DateTaken.Value), Field.Store.YES, Field.Index.ANALYZED));
                        }
                        else
                        {
                            doc.Add(new NumericField("EPOCHTIME", Field.Store.YES, true)
                                .SetLongValue(0));
                            // doc.AddCommon(new Field("EPOCHTIME", string.Empty, Field.Store.YES, Field.Index.NOT_ANALYZED));
                        }

                        lock (lockObject)
                        {
                            results.Add(doc);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.SendLogAsync($"Failed to get Document for :{photoPath}. Errpr: {ex.Message}");
                        _logger.SendLogWithException("More details-", ex);
                    }
                }));
            } // for loop
            await Task.WhenAll(taskArray);
            if(badFiles.Count> 0)
            {
                Utils.WriteThreadSafe(_appSettings.BadFilesName, badFiles.ToArray(), _logger, _appSettings);
            }
            return results;
        }

        public static string GetHeightTag(double altitude)
        {
            if (altitude == 0)
            {
                return string.Empty;
            }
            double feets = 3.28084 * altitude;
            int inThousands = (int)feets / 1000;
            if (inThousands == 0)
            {
                inThousands = 1;
            }
            return $"{inThousands.ToString("0")}kfeet";
        }

        public static Document CreatePhotoDocument(
            string pathKey,
            string allTags, 
            string keyword, 
            string location, 
            string dateTimeKeyword, 
            string thumbNail)
        {
            Document doc = new Document();
            doc.Add(new Field("DocType", "PhotoDocument", Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("PathKey", Utils.GetUniquePathKey(pathKey), Field.Store.YES, Field.Index.NOT_ANALYZED));

            doc.Add(new Field("Tags", allTags, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("KeyWords", keyword.Trim(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Location", location.Trim(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("DateTimeKeywords", dateTimeKeyword.Trim(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("ThumbNail", thumbNail, Field.Store.YES, Field.Index.NOT_ANALYZED));
            return doc;
        }
        public static Document CreateViewDocument(
            string pathKey,
            string stringContent)
        {
            Document doc = new Document();
            doc.Add(new Field("DocType", "ViewDocument", Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("PathKey", Utils.GetUniquePathKey(pathKey), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("ContentKey", stringContent, Field.Store.YES, Field.Index.NOT_ANALYZED));
            return doc;
        }
        public static Document CreateVersionDocument(
            string pathKey,
            string version)
        {
            Document doc = new Document();
            doc.Add(new Field("DocType", "VersionDocument", Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("PathKey", Utils.GetUniquePathKey(pathKey), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Version", version, Field.Store.YES, Field.Index.NOT_ANALYZED));
            return doc;
        }
        private static string RemoveRedundantKeywords(string dateTimeKeywords)
        {
            if(string.IsNullOrEmpty(dateTimeKeywords))
            {
                return dateTimeKeywords;
            }
            for (int i = 1; i <= 12; i++)
            {
                string monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i);
                dateTimeKeywords = dateTimeKeywords.Replace(monthName, string.Empty);
            }
            string[] parts = dateTimeKeywords.Split(' ');
            string result = string.Empty;
            foreach (var item in parts)
            {
                if(!Utils.IsEntirelyNumericString(item))
                {
                    result += " " + item;
                }
            }

            return result.Trim();
        }
        public static async Task<string> GetImageSizeAsync(string filePath)
        {
            return await Task.Run(()=> {
                IReadOnlyList<MetadataExtractor.Directory> directories = null;
                try
                {
                    directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);
                }
                catch (Exception ex)
                {
                    _logger.SendDebugLogAsync(string.Format($"Couldn't get metadata for: {filePath}. Err: {ex.Message}"));
                    return string.Empty;
                }

                // PrintAllDir(directories);

                var subIfdDirectories = directories?.OfType<JpegDirectory>();
                string width = string.Empty;
                string height = string.Empty;
                foreach (JpegDirectory dir in subIfdDirectories)
                {
                    var text = dir?.GetDescription(JpegDirectory.TagImageWidth);
                    if (text == null)
                    {
                        continue;
                    }
                    width = text;
                }
                foreach (JpegDirectory dir in subIfdDirectories)
                {
                    var text = dir?.GetDescription(JpegDirectory.TagImageHeight);
                    if (text == null)
                    {
                        continue;
                    }
                    height = text;
                }
                return $"w{width}h{height}";
            });
        }

        public static async Task<PhotoMeta> GetPhotoMetaAsync(string photoPath, bool thumnail = true )
        {
            PhotoMeta result = new PhotoMeta();
            try
            {
                string scaledFilePath = string.Empty;
                var thumbNailTask = Task.Run(() =>
                {
                    if (thumnail)
                    {
                        try
                        {
                            result.ThumbNail = Photo.ImageToString(photoPath,  _logger);
                        }
                        catch (Exception ex)
                        {
                            _logger.SendLogAsync($"Resizing failed. File: {photoPath}, scaled down path: {scaledFilePath} Error: {ex.Message}");
                            _logger.SendLogWithException("Resizing failed", ex);
                            result.ThumbNail = string.Empty;
                        }
                    }
                });

                IReadOnlyList<MetadataExtractor.Directory> directories = null;
                directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(photoPath);
                var miscTask = Task.Run(() =>
                {
                    var subIfdDirectories = directories?.OfType<ExifIfd0Directory>();
                    foreach (ExifIfd0Directory dir in subIfdDirectories)
                    {
                        var text = dir?.GetDescription(ExifIfd0Directory.TagWinComment);
                        if (text != null)
                        {
                            result.Comment = text;
                        }
                        text = dir?.GetDescription(ExifIfd0Directory.TagWinKeywords);
                        if (text != null)
                        {
                            if (text.Contains("with"))
                            {
                                // here
                            }
                            result.CustomeKeyWords = text;
                        }
                        text = dir?.GetDescription(ExifIfd0Directory.TagMake);
                        if (text != null)
                        {
                            result.CameraMake = text;
                            result.CameraMake = ImproveMakeName(result.CameraMake);

                        }
                        text = dir?.GetDescription(ExifIfd0Directory.TagModel);
                        if (text != null)
                        {
                            result.CameraModel = text;
                            result.CameraModel = ExtractOnlyModel(result.CameraMake, result.CameraModel);
                        }
                    }
                });


                var gpsTask = Task.Run(() =>
                {
                    // GPS data
                    var gpsDirs = directories?.OfType<GpsDirectory>();
                    foreach (var item in gpsDirs)
                    {
                        var geoLocation = item.GetGeoLocation();
                        if (geoLocation != null && !geoLocation.IsZero)
                        {
                            result.Longitude = Math.Round(geoLocation.Longitude, 3);
                            result.Lattitude = Math.Round(geoLocation.Latitude,  3);
                        }
                        else
                        {
                            continue;
                        }

                        try
                        {
                            result.Altitude = item.GetDouble(GpsDirectory.TagAltitude);
                        }
                        catch (Exception )
                        {
                            _logger.SendDebugLogAsync($"Altitude info not available, file: {photoPath}");
                            continue;
                        }
                    }
                });

                var dateTimeTask = Task.Run(() =>
                {
                    // Date taken
                    var subIfDirs = directories?.OfType<ExifSubIfdDirectory>();
                    foreach (ExifSubIfdDirectory dir in subIfDirs)
                    {
                        var dateTime = dir?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                        if (dateTime == null)
                        {
                            continue;
                        }
                        try
                        {
                            result.DateTaken = DateTime.ParseExact(dateTime, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                        }
                        catch (Exception)
                        {
                            result.DateTaken = null;
                        }
                    }
                });
                await Task.WhenAll(new[] {
                        miscTask,
                        // keyWordTask,
                        gpsTask,
                        dateTimeTask,
                        thumbNailTask
                    });
                // Don't put just make when model not available.
                if (string.IsNullOrEmpty(result.CameraModel))
                {
                    result.CameraMake = string.Empty;
                }

            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"GetPhotoMetaAsync - error {ex.Message}, file: {photoPath}");
            }
            return result;
        }

        private static string ImproveMakeName(string cameraMake)
        {
            string[] parts = cameraMake.Split(' ');
            if (!string.IsNullOrEmpty(parts[0]))
            { 
                return parts[0].Trim(new char[] {'"'}).Trim();
            }
            if (!string.IsNullOrEmpty(parts[1]))
            {
                return parts[1].Trim(new char[] { '"' }).Trim();
            }
            return string.Empty;
        }

        private static string ExtractOnlyModel(string cameraMake, string cameraModel)
        {
            string[] parts = cameraModel.Split(' ');
            foreach (var item in parts)
            {
                if(Utils.DoesContainsNumbers(item))
                {
                    return item.Trim();
                }
            }
            return string.Empty;
        }

        private static void PrintAllDir(IReadOnlyList<MetadataExtractor.Directory> directories)
        {
            foreach (var directory in directories) 
            {
                foreach (var tag in directory.Tags)
                {
                    _logger.SendLogAsync($"{directory.Name} - {tag.Name} = {tag.Description}");
                }
            }
        }

        public async static Task<string> GetDateTimeTagsAsync(DateTime? date)
        {
            if(date == null)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            string strMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.Value.Month);
            string yearName = date.Value.Year.ToString();
            await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
            {
                Recepient = RecepientType.TIME_DATA,
                Message = strMonthName + ":" + yearName
            });

            // Get Year, Month, Date, and Full date, time.
            sb.Append(DetermineAutoTAGs(date.Value));
            sb.Append(" ");
            return sb.ToString();
        }

        internal async static Task<bool> AnyMissingFromNew(string fullPath, string empty, string localThreadSafeCustomMeta)
        {
            var meta = await GetPhotoMetaAsync(fullPath, false);
            if(meta.CustomeKeyWords.IndexOf(localThreadSafeCustomMeta) != -1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string DetermineAutoTAGs(DateTime dateTime)
        {
            string[] dayShortnames = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;

            // string inp = dateTime.ToLongDateString();
            string strMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTime.Month);
            string monthShortName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(dateTime.Month);
            string tags = dateTime.Year.ToString()
                + " " + monthShortName
                + " " + strMonthName
                + " " + dayShortnames[(int)dateTime.DayOfWeek] // Short day name
                + " " + dateTime.DayOfWeek
                + " " + DetermineSeason(dateTime)
                + " " + DetermineWeekDayWeekEndDayNight(dateTime)
                + " " + Utils.DayString(dateTime.Day);
            return tags;
        }

        private static string DetermineWeekDayWeekEndDayNight(DateTime dateTime)
        {
            String result = string.Empty;

            if (dateTime.DayOfWeek == DayOfWeek.Saturday || dateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                result += " Weekend ";
            }
            else
            {
                result += " Weekday ";
            }
            var hourAm_Pm = dateTime.ToString("tt", CultureInfo.InvariantCulture);
            string hourFormat12 = dateTime.ToString("hh");
            result += " " + hourFormat12.TrimStart('0');
            result += hourAm_Pm;

            string hourFormat24 = dateTime.ToString("HH");
            int hourDay = Int32.Parse(hourFormat24);

            //if (hourDay >= 00 && hourDay < 12)
            //{
            //    result += hourDay.ToString() + "AM";
            //}
            //if (hourDay >= 12 && hourDay < 24)
            //{
            //    if (hourDay > 12)
            //    {
            //        result += (hourDay - 12).ToString() + "PM";
            //    }
            //    else
            //    {
            //        result += (hourDay).ToString() + "PM";
            //    }
            //}


            if (hourDay >= 6 && hourDay < 12)
            {
                result += " Morning ";
            }
            if (hourDay == 12)
            {
                result += " Noon";  
            }
            if (hourDay > 12 && hourDay < 17)
            {
                result += " Afternoon";
            }
            if (hourDay >= 17 && hourDay < 20)
            {
                result += " Evening";
            }
            if (hourDay >= 20 && hourDay <= 24)
            {
                result += " Night";
            }
            if (hourDay >= 0 && hourDay < 6)
            {
                result += " Night";
            }
            return result;
        }

        private static string DetermineSeason(DateTime date)
        {
            int doy = date.DayOfYear - Convert.ToInt32((DateTime.IsLeapYear(date.Year)) && date.DayOfYear > 59);
            string currentSeason = String.Format("{0}", ((doy < 80 || doy >= 355) ? "winter" : ((doy >= 80 && doy < 172) ? "spring" : ((doy >= 172 && doy < 266) ? "summer" : "fall"))));
            return currentSeason;
        }

        public static async Task AppendExifWrapperAsync(string fileName, string comment, string tags)
        {
            var photoMeta = await GetPhotoMetaAsync(fileName, false);
            tags += " " + TagsHelper.NormalizeTAGs(photoMeta.CustomeKeyWords, false, true, 2, true, true, -1).ToLower();
            await PhotoManipulation.SetExifWrapperAsync(fileName, string.Empty, tags);
        }
        public static async Task RemovExifWrapperAsync(string fileName, string comment, string tagsToRemove)
        {
            var photoMeta = await GetPhotoMetaAsync(fileName, false);
            
            // Remove tags that used wants to remove.
            var currentTags = photoMeta.CustomeKeyWords;
            string[] words = tagsToRemove.Split(new char[] {' ',',',';'},StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string word in words)
            {
                currentTags = currentTags.Replace(word.Trim(), "");
            }

            currentTags = TagsHelper.NormalizeTAGs(currentTags, false, true, 2, true, true, -1).ToLower();
            if(currentTags == TagsHelper.NormalizeTAGs(photoMeta.CustomeKeyWords, false, true, 2, true, true, -1).ToLower())
            {
                return;
            }
            if(!string.IsNullOrEmpty(currentTags))
            {
                await PhotoManipulation.SetExifWrapperAsync(fileName, string.Empty, currentTags);
            }
            else
            {
                await PhotoManipulation.EraseCustomTagWrapperAsync(new string[] {fileName});
            }
        }

        private static async Task SetExifWrapperAsync(string fileName, string comment, string tags)
        {
            await Task.Run(() =>
            {
                try
                {

                    Utils.MakeWritable(fileName);
                    using (MemoryStream memory = new MemoryStream())
                    {
                        if (!string.IsNullOrEmpty(comment))
                        {
                            comment = TagsHelper.NormalizeTAGs(comment, false, true, 2, true, true, -1).ToLower();
                            comment = comment.Replace(" ", "; ");
                        }
                        if (!string.IsNullOrEmpty(tags))
                        {
                            tags = TagsHelper.NormalizeTAGs(tags, false, true, 2, true, true, -1).ToLower();
                            tags = tags.Replace(" ", "; ");
                        }
                        using (Image image = SetEXIFDataInternal(fileName, comment, tags))
                        {
                            image.Save(memory, ImageFormat.Jpeg);
                            // image.Save(System.IO.Path.GetTempPath()  + Path.GetFileName(fileName));
                        }
                        //File.Delete(fileName);
                        //File.Move(System.IO.Path.GetTempPath() + Path.GetFileName(fileName), fileName);
                        byte[] bytes = memory.ToArray();
                        using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }

                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"SetExifWrapper Failed Error: {ex.Message}. File:{fileName}");
                }
            });
        }
        //public static async Task<string> GetEXIFDataAsync(string fileName, bool combineTagAndComment = true)
        //{
        //    string comments = string.Empty;
        //    string tags = string.Empty;
        //    if (!File.Exists(fileName))
        //    {
        //        throw new FileNotFoundException("Image file not found.", fileName);
        //    }
        //    if (string.Compare(Path.GetExtension(fileName), ".jpg", true) != 0)
        //    {
        //        return string.Empty;
        //    }

        //    // PropertyItem pi = null;
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            return GetCommentKeywords(fileName, combineTagAndComment);
        //            //using (Bitmap image = FileToBitmap(fileName))
        //            //{
        //            //    try
        //            //    {
        //            //        pi = image.GetPropertyItem(40092);
        //            //        comments = Encoding.Unicode.GetString(pi.Value).Trim('\0');
        //            //    }
        //            //    catch { comments = string.Empty; }

        //            //    try
        //            //    {
        //            //        pi = image.GetPropertyItem(40094);
        //            //        tags = Encoding.Unicode.GetString(pi.Value).Trim('\0');
        //            //    }
        //            //    catch { tags = string.Empty; }
        //            //}
        //        }
        //        catch (Exception ex)
        //        {
        //            __logger.SendLogAsync($"GetEXIFDataAsync. Error: {ex.Message}, File: {fileName}");
        //            return string.Empty;
        //        }

        //    });
        //    //if (combineTagAndComment == true)
        //    //{
        //    //    return tags + " " + comments;
        //    //}
        //    //else
        //    //{
        //    //    return tags;
        //    //}
        //}
        private static Image SetEXIFDataInternal(string fileName, string comments, string tags)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("Image file not found.", fileName);
            }

            PropertyItem pi = null;
            byte[] data = null;

            tags = tags.Replace(',', ';');

            Image image = Image.FromFile(fileName);

            if (!string.IsNullOrEmpty(comments))
            {
                data = Encoding.Unicode.GetBytes(comments);

                try
                {
                    pi = image.GetPropertyItem(40092);
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }
                catch
                {
                    pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                    pi.Id = 40092;
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }

                image.SetPropertyItem(pi);
            }

            if (!string.IsNullOrEmpty(tags))
            {
                data = Encoding.Unicode.GetBytes(tags);

                try
                {
                    pi = image.GetPropertyItem(40094);
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }
                catch
                {
                    pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                    pi.Id = 40094;
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }

                image.SetPropertyItem(pi);
            }
            return image;
        }

        public static Bitmap DEL_SetEXIFData(string fileName, string comments, string tags)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("Image file not found.", fileName);
            }

            Bitmap result = null;
            PropertyItem pi = null;
            byte[] data = null;

            tags = tags.Replace(',', ';');

            using (Bitmap image = new Bitmap(fileName))
            {
                result = (Bitmap)image.Clone();

                if (!string.IsNullOrEmpty(comments))
                {
                    data = Encoding.Unicode.GetBytes(comments);

                    try
                    {
                        pi = image.GetPropertyItem(40092);
                        pi.Len = data.Length;
                        pi.Type = 1;
                        pi.Value = data;
                    }
                    catch
                    {
                        pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                        pi.Id = 40092;
                        pi.Len = data.Length;
                        pi.Type = 1;
                        pi.Value = data;
                    }

                    result.SetPropertyItem(pi);
                }

                if (!string.IsNullOrEmpty(tags))
                {
                    data = Encoding.Unicode.GetBytes(tags);

                    try
                    {
                        pi = image.GetPropertyItem(40094);
                        pi.Len = data.Length;
                        pi.Type = 1;
                        pi.Value = data;
                    }
                    catch
                    {
                        pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                        pi.Id = 40094;
                        pi.Len = data.Length;
                        pi.Type = 1;
                        pi.Value = data;
                    }

                    result.SetPropertyItem(pi);
                }
            }

            return result;
        }
        public static async Task EraseCustomTagWrapperAsync(string[] fileNames)
        {
            List<Task> taskArray = new List<Task>();
            foreach (var file in fileNames)
            {
                try
                {
                    taskArray.Add(Task.Run(() =>
                    {
                        Utils.MakeWritable(file);
                        using (MemoryStream memory = new MemoryStream())
                        {
                            using (Bitmap image = EraseCustomTag(file))
                            {
                                image.Save(memory, ImageFormat.Jpeg);
                            }
                            byte[] bytes = memory.ToArray();
                            using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.ReadWrite))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                            }
                        }
                    }));
                }
                catch (Exception ex)
                {
                    _logger.SendLogAsync($"Couldn't erase custom Keyword from flie:{file}. Error: {ex.Message}. Moving to next file.");
                }
            }
            await Task.WhenAll(taskArray);
        }
        public static Bitmap EraseCustomTag(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("Image file not found.", fileName);
            }

            Bitmap result = null;
            PropertyItem pi = null;
            byte[] data = null;
            using (Bitmap image = new Bitmap(fileName))
            {
                result = (Bitmap)image.Clone();
                data = Encoding.Unicode.GetBytes(string.Empty);
                try
                {
                    pi = image.GetPropertyItem(40094);
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }
                catch
                {
                    pi = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));
                    pi.Id = 40094;
                    pi.Len = data.Length;
                    pi.Type = 1;
                    pi.Value = data;
                }
                result.SetPropertyItem(pi);
            }

            return result;
        }

        public static string[] GatherAllTimeWordsToExclude()
        {
            IEnumerable<string> result = new List<string>();
            result = result.Concat(AllSeasonsOfYear());
            //result = result.Concat(AllMonthsOfYear());
            result = result.Concat(AllDayNames());
            // result = result.Concat(excludeDaySegment);
            result = result.Concat(new string[] {
                    "Weekend",
                    "Weekday",
                    "Morning",
                    "Noon",
                    "Afternoon",
                    "Evening",
                    "Night",
                    });
            return result.ToArray();
        }
        public static string[] AllDayNames()
        {
            string[] result = new string[]
            {
                "Sunday",
                "Monday",
                "Tuesday",
                "Wednesday",
                "Thursday",
                "Friday",
                "Saturday",
            };
            return result;
        }
        public static string[] AllSeasonsOfYear()
        {
            List<string> seasons = new List<string>();
            seasons.Add("winter");
            seasons.Add("spring");
            seasons.Add("summer");
            seasons.Add("fall");
            return seasons.ToArray();
        }
        public static string[] AllMonthsOfYear()
        {
            List<string> months = new List<string>();
            for (int i = 1; i <= 12; i++)
            {
                months.Add(CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i));
            }
            return months.ToArray();
        }
        public static string[] AllDaysOfWeek()
        {
            List<string> days = new List<string>();
            for (int i = 1; i <= 31; i++)
            {
                days.Add(Utils.DayString(i));
            }
            return days.ToArray();
        }

        public static string GetSuggestedTagsFromFolderName(string filePath)
        {
            String pathFragmentToConsider = String.Empty;
            string extension = System.IO.Path.GetExtension(filePath);

            // Chop drive name
            string pathWithoughDriveName = filePath.Substring(Path.GetPathRoot(filePath).Length);

            // Chop extension
            pathWithoughDriveName = pathWithoughDriveName.Substring(0, pathWithoughDriveName.Length - extension.Length);
            String[] pathFragment = pathWithoughDriveName.Split(new char[] { '\\' });
            //if (pathFragment.Length > 0)
            //{ 
            //    pathFragmentToConsider += " " + pathFragment[pathFragment.Length - 1]; 
            //}
            if (pathFragment.Length > 1)
            {
                pathFragmentToConsider += " " + pathFragment[pathFragment.Length - 2];
            }
            var removedNumeric = TagsHelper.FilterNumericTags(pathFragmentToConsider);
            // var brokenCamelCase = TagsHelper.BreakCameCaseWords(removedNumeric);
            return TagsHelper.NormalizeTAGs(removedNumeric, false, true, 2, true, true, -1).ToLower();
        }

        public static async Task<string> GetSuggestedTagsFromGeoDataAsync(double longitude, double lattitude, string clientId)
        {
            if(longitude == 0 && lattitude == 0)
            {
                return string.Empty;
            }
            // using (StopWatchInternal sw = new StopWatchInternal("GetSuggestedTagsFromGeoDataAsync", _logger))
            {
                string gpsKey = longitude.ToString().TrimStart('-') + "," + lattitude.ToString().TrimStart('-');
                LocationCache gpsCache = new LocationCache(_appSettings.GpsDBFileName, _logger, _appSettings);
                await gpsCache.LoadAsync();

                string location = gpsCache.GetValue(gpsKey);
                if (string.IsNullOrEmpty(location))
                {
                    _logger.SendDebugLogAsync($"Cache miss: {gpsKey}");
                    // StopWatchInternal swFetch = new StopWatchInternal("FetchLocation", _logger);
                    location = await FetchLocation(longitude, lattitude, clientId);//.Result.Trim();
                    // swFetch.Dispose();
                    if (string.IsNullOrEmpty(location.Trim()))
                    {
                        return string.Empty;
                    }
                    await MetaQueue<MetaQueueMessage>.EnQueueAsync(new MetaQueueMessage()
                    {
                        Recepient = RecepientType.GPS_DATA,
                        Message = gpsKey + ":" + location
                    });
                }
                return location;
            }
        }

        private static async Task<string> FetchLocation(double longitude, double latitude, string clientId)
        {

            HttpClient client = new HttpClient();
            string content = string.Empty;
            try
            {
                HttpResponseMessage result = await client.GetAsync(new Uri("http://www.getpitara.com/geo/server.php?latitude=" + latitude + "&longitude=" + longitude + "&clientid=" + clientId));
                if (!result.IsSuccessStatusCode)
                {
                    _logger.SendDebugLogAsync($"Error fetching location info.");
                    return string.Empty;
                }
                content = await result.Content.ReadAsStringAsync();
                JSON_RESULT json_result = Newtonsoft.Json.JsonConvert.DeserializeObject<JSON_RESULT>(content);
                if (json_result.success)
                {
                    string str_result = json_result.result;
                    dynamic jsonObject = JsonConvert.DeserializeObject(str_result);
                    string address = string.Empty;
                    address += " " + jsonObject?.Response?.View[0]?.Result[0]?.Location.Address.City.ToString();
                    // address += "," + jsonObject?.Response?.View[0]?.Result[0]?.Location.Address.State.ToString();
                    address += "," + jsonObject?.Response?.View[0]?.Result[0]?.Location.Address.Country.ToString();

                    // Let's pick state name.
                    if (jsonObject.Response.View[0]?.Result[0]?.Location.Address.AdditionalData != null)
                    {
                        for (int i = 0; i < jsonObject.Response.View[0]?.Result[0]?.Location.Address.AdditionalData.Count; i++)
                        {
                            if (jsonObject.Response.View[0]?.Result[0]?.Location.Address.AdditionalData[i].key == "StateName")
                            {
                                address += "," + jsonObject.Response.View[0]?.Result[0]?.Location.Address.AdditionalData[i].value;
                            }
                        }
                    }
                    return address;
                }
                else
                {
                    _logger.SendDebugLogAsync($"Error fetching location info.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.SendDebugLogAsync($"Can not get location info. Error: {ex.Message}");
                return string.Empty;
            }
        }

        private static string ToEpochTime(DateTime dateTime)
        {
            TimeSpan t = dateTime - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            return secondsSinceEpoch.ToString();
        }
        public static string HashOfPhotoAsync(string file)
        {
            return string.Empty;
            //bool isJPG = false;
            //try
            //{
            //    if (IsFileJPEGType(file))
            //    {
            //        isJPG = true;
            //        return await HashOfJPEGNewWayAsync(file);
            //    }
            //    else
            //    {
            //        return await InternalHashFromAnyFileAsync(file);
            //    }
            //}
            //catch (Exception ex)
            //{
            //    string err = string.Empty;
            //    if (true == isJPG)
            //    {
            //        err = string.Format($"JPEG hash not possible. Hashing as regular file. Error: {ex.Message}- file: {file}");
            //        _logger.SendLogAsync(err);
            //        return await InternalHashFromAnyFileAsync(file);
            //    }
            //    else
            //    {
            //        _logger.SendLogWithException(err, ex);
            //        return string.Empty;
            //    }
            //}
        }
        private static bool IsFileJPEGType(string file)
        {
            var ext = System.IO.Path.GetExtension(file).ToLower();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static async Task<string> InternalHashFromAnyFileAsync(string file)
        {
            return await Task.Run(() =>
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    int length = Convert.ToInt32(fs.Length);
                    byte[] data = new byte[length];
                    fs.Read(data, 0, length);
                    return ByteToHash(data);
                }
            });
        }
        public static string ByteToHash(byte[] data)
        {
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                byte[] hash = md5.ComputeHash(data);
                md5.Clear();
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }


        public static byte[] HMACSHA256Hash(String data, String key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key)))
            {
                return hmac.ComputeHash(Encoding.ASCII.GetBytes(data));
            }
        }
        public static List<bool> GetHash(Bitmap bmpSource)
        {
            List<bool> lResult = new List<bool>();
            //create new image with 16x16 pixel
            using (Bitmap bmpMin = new Bitmap(bmpSource, new Size(16, 16)))
            {
                for (int j = 0; j < bmpMin.Height; j++)
                {
                    for (int i = 0; i < bmpMin.Width; i++)
                    {
                        //reduce colors to true / false                
                        lResult.Add(bmpMin.GetPixel(i, j).GetBrightness() < 0.5f);
                    }
                }
                return lResult;
            }
        }
        //private static async Task<string> _HashOfJPEGAsync(string file)
        //{
        //    return await Task.Run(() =>
        //    {
        //        try
        //        {
        //            using (Bitmap image = new Bitmap(file))
        //            {
        //                List<bool> list = GetHash(image);
        //                var newList = list.Select(x => x.ToString().ToLower()[0]);
        //                var baseString = string.Join(string.Empty, newList.ToArray());
        //                var amededBaseSTring = $"{baseString}w{image.Width}h{image.Height}";
        //                return Utils.GetHashString(amededBaseSTring);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.SendLogAsync($"HashOfJPEGAsync. error: {ex.Message} File: {file}");
        //            return string.Empty;
        //        }
        //    });
        //}
        public static MemoryStream FileToStream(string file)
        {
            using (Stream fileStream = System.IO.File.Open(file,
                System.IO.FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, bytes.Length);
                fileStream.Close();
                return new MemoryStream(bytes);
            }
        }
        public static async Task<string> HashOfJPEGNewWayAsync(string file)
        {
            return await Task.Run(async () =>
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        string siezeInfo = await GetImageSizeAsync(file);
                        // using (Stream stream = FileToStream(file))
                        {
                            using (Image image = Image.FromFile(file))
                            {
                                using (Bitmap bmpScaled = new Bitmap(image, new Size(16, 16)))
                                {
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        bmpScaled.Save(ms, ImageFormat.Bmp);

                                        byte[] stringBytes = Encoding.UTF8.GetBytes(siezeInfo);

                                        List<byte> byteList = stringBytes.Concat(ms.ToArray()).ToList();
                                        var returnVal = ByteToHash(byteList.ToArray());
                                        stringBytes = null;
                                        byteList = null;

                                        ms.Dispose();
                                        bmpScaled.Dispose();
                                        // bmpScaled = null;
                                        image.Dispose();
                                        // image = null;
                                        // stream.Close();
                                        // stream.Dispose();
                                        // bmpScaled = null;
                                        return returnVal;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        attempt++;
                        // Try 3 times. 1 2 3.
                        if (attempt < 4)
                        {
                            _logger.SendLogAsync($"Hashing the photo attemt: {attempt}");
                            continue;
                        }
                        _logger.SendDebugLogAsync($"HashOfJPEGNewWayAsync failed. Error:{ex.Message}. File:{file}");
                        throw;
                    }
                }
            });
        }
        public static string FormatToolTip(string keyword, string geoLoc)
        {
            if (string.IsNullOrEmpty(keyword) && string.IsNullOrEmpty(geoLoc))
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.Replace(";", " ");
                keyword = keyword.Replace(",", " ");
                var keywordArray = keyword.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var upperCase = keywordArray.Select(x => TagsHelper.UppercaseFirst(x));
                keyword = string.Join(";", upperCase);
            }
            else
            {
                keyword = string.Empty;
            }
            string result = string.Empty;
            if (!string.IsNullOrEmpty(geoLoc))
            {
                result = keyword + ";" + geoLoc;
            }
            else
            {
                result = keyword;
            }
            result = result.Replace(',', ';');
            result = result.Replace("; ", ";");
            result = result.Replace(";", "; ");
            result = result.Trim().TrimEnd(new char[] { ';' });
            result = result.Trim().TrimStart(new char[] { ';' });
            return result.Trim();
        }
        public static Bitmap ReadBitmapFromFile(String s_Path)
        {
            using (FileStream i_Stream = new FileStream(s_Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (Bitmap i_Bmp = new Bitmap(i_Stream))
                {
                    return new Bitmap(i_Bmp);
                }
            }
        }
        public static Bitmap ResizeImage(double askedWidth, string file)
        {
            if (!File.Exists(file))
            {
                _logger.SendLogAsync($"File not found: {file}");
                throw new Exception($"File not found: {file}");
            }

            const int OrientationKey = 0x0112;
            const int NormalOrientation = 1;
            const int MirrorHorizontal = 2;
            const int UpsideDown = 3;
            const int MirrorVertical = 4;
            const int MirrorHorizontalAndRotateRight = 5;
            const int RotateLeft = 6;
            const int MirorHorizontalAndRotateLeft = 7;
            const int RotateRight = 8;
            string resizedPath = string.Empty;
            bool isRotation = false;
            try
            {
                using (System.Drawing.Image image = Image.FromFile(file))
                {
                    double newWidth = ((double)image.Width > askedWidth) ? askedWidth : (double)image.Width;
                    double ratio = (double)image.Width / newWidth;
                    double newHeight = (double)image.Height / ratio;

                    var destRect = new System.Drawing.Rectangle(0, 0, (int)newWidth, (int)newHeight);
                    var scaledImage = new Bitmap((int)newWidth, (int)newHeight);
                    scaledImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
                    using (var graphics = Graphics.FromImage(scaledImage))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        using (var wrapMode = new ImageAttributes())
                        {
                            wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                            graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                        }

                        // Let's set orientation from EXIF header.
                        // Fix orientation if needed.
                        if (image.PropertyIdList.Contains(OrientationKey))
                        {
                            var orientation = (int)image.GetPropertyItem(OrientationKey).Value[0];
                            switch (orientation)
                            {
                                case NormalOrientation:
                                    // No rotation required.
                                    break;
                                case MirrorHorizontal:
                                    scaledImage.RotateFlip(RotateFlipType.RotateNoneFlipX);
                                    break;
                                case UpsideDown:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                    break;
                                case MirrorVertical:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate180FlipX);
                                    break;
                                case MirrorHorizontalAndRotateRight:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate90FlipX);
                                    isRotation = true;
                                    break;
                                case RotateLeft:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                    isRotation = true;
                                    break;
                                case MirorHorizontalAndRotateLeft:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate270FlipX);
                                    isRotation = true;
                                    break;
                                case RotateRight:
                                    scaledImage.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                    isRotation = true;
                                    break;
                                default:
                                    {
                                        // throw new NotImplementedException("An orientation of " + orientation + " isn't implemented.");
                                        _logger.SendDebugLogAsync(string.Format("An orientaton of {0} not implemented", orientation));
                                        break;
                                    }
                            }
                        }
                        if (isRotation == true)
                        {
                        }
                        image.Dispose();
                        // image = null;
                        return scaledImage;
                    } // garphics
                }
            }
            catch (Exception ex)
            {
                _logger.SendDebugLogAsync($"Error in resizing. Will be retried later. Error:{ex.Message}, file:{file}");
                throw;
            }
        }

        public static Bitmap TextOverlay(string fileName, string text, Font font, Brush brush, StringAlignment horizontalAlignment = StringAlignment.Far, StringAlignment verticalAlignment = StringAlignment.Near, StringTrimming trimming = StringTrimming.EllipsisCharacter)
        {
            if (!File.Exists(fileName)) throw new FileNotFoundException("Image file not found.", fileName);

            Bitmap result = null;

            using (Bitmap image = new Bitmap(fileName))
            {
                result = new Bitmap(image.Width, image.Height);

                using (Graphics g = Graphics.FromImage(result))
                {
                    // Highest rendering quality
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.High;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    g.DrawImage(image, 0, 0, image.Width, image.Height);

                    using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                    {
                        sf.Alignment = horizontalAlignment;
                        sf.LineAlignment = verticalAlignment;
                        sf.Trimming = trimming;

                        g.DrawString(text, font, brush, new Rectangle(0, 0, result.Width, result.Height), sf);
                    }
                }
            }

            return result;
        }

        public static async Task DONOTUSE__ONLY_TO_GENERATE_TEST_DATA_OverWriteImage(string file, string logoFile)
        {
            string tempFileName = "tempFile.jpg";
            using (Image img = Image.FromFile(file))
            {
                using (Graphics gr = Graphics.FromImage(img))
                {
                    gr.Clear(Color.White);
                    // Make a brush that contains the original image.
                    using (Brush brush = new TextureBrush(new Bitmap(logoFile)))
                    {
                        // Fill the selected area.
                        gr.FillRectangle(brush, new Rectangle(0, 500, img.Width, img.Height));
                    }
                }
                img.Save(tempFileName);
            }
            using (Image img = Image.FromFile(tempFileName))
            {
                using (Graphics gr = Graphics.FromImage(img))
                {
                    using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
                    {
                        StringAlignment horizontalAlignment = StringAlignment.Far;
                        StringAlignment verticalAlignment = StringAlignment.Near;
                        StringTrimming trimming = StringTrimming.EllipsisCharacter;
                        sf.Alignment = horizontalAlignment;
                        sf.LineAlignment = verticalAlignment;
                        sf.Trimming = trimming;

                        var photoMeta = await PhotoManipulation.GetPhotoMetaAsync(file);
                        string dateTimeTag = string.Empty;
                        if (photoMeta.DateTaken != null)
                        {
                            dateTimeTag = DetermineAutoTAGs(photoMeta.DateTaken.Value);
                        }
                        var longi = photoMeta.Longitude;
                        var latti = photoMeta.Lattitude;

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"Date: {dateTimeTag}");
                        sb.AppendLine($"Longitude: {longi}");
                        sb.AppendLine($"Lattitude: {latti}");
                        Font font = new Font(SystemFonts.MenuFont.FontFamily, 25.0f);

                        gr.DrawString(sb.ToString(), font, Brushes.DodgerBlue, new Rectangle(0, 0, img.Width, img.Height), sf);
                    }

                }
                img.Save(file);
            }
            File.Delete(tempFileName);
        }
        public static  Bitmap MakeImageWithArea(Bitmap source_bm, List<Point> points)
        {
            // Copy the image.
            Bitmap bm = new Bitmap(source_bm.Width, source_bm.Height);

            // Clear the selected area.
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.Clear(Color.Transparent);

                // Make a brush that contains the original image.
                using (Brush brush = new TextureBrush(source_bm))
                {
                    // Fill the selected area.
                    gr.FillPolygon(brush, points.ToArray());
                }
            }
            return bm;
        }

    }
}


