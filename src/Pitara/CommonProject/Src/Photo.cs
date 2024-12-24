// using Pitara.PhotoStuff;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommonProject.Src
{
    public class Photo : INPCBase
    {
        public string EpochTime { get; set; }
        public static int ThumbNailSieze { get; set; } = 150;
        public int IndexLocation { get; set; }
        string _heading;
        public string Heading
        {
            get => _heading ?? _heading;
            set
            {
                if (_heading != value)
                {
                    _heading = value;
                    base.NotifyChanged("Heading");
                }
            }

        }
        System.Windows.Media.Color _headingBackground;
        public System.Windows.Media.Color HeaderBackground
        {
            get => _headingBackground;
            set
            {
                if (_headingBackground != value)
                {
                    _headingBackground = value;
                    base.NotifyChanged("HeaderBackground");
                }
            }

        }

        string _tooltips;
        public string ToolTips
        {
            get => _tooltips ?? _tooltips;
            set
            {
                if (_tooltips != value)
                {
                    _tooltips = value;
                    base.NotifyChanged("ToolTips");
                }
            }

        }

        ImageSource _image;
        public ImageSource Image
        {
            // Data virtualization
            // Image only constructed on get and not existing, lazy loading ...
            get => _image ?? _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    base.NotifyChanged("Image");
                }
            }

        }
        public static ImageSource GetThumbNail(string file)
        {
            System.Drawing.Image image = System.Drawing.Image.FromFile(file);
            System.Drawing.Image thumb = image.GetThumbnailImage(120, 120, () => false, IntPtr.Zero);

            using (var ms = new MemoryStream())
            {
                thumb.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.DecodePixelWidth = 120;
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                
                image.Dispose();
                thumb.Dispose();

                return bitmapImage;
            }
        }
        public static BitmapImage FromString(string imageString)
        {
            if(string.IsNullOrEmpty(imageString))
            {
                return null;
            }
            var bytes = Convert.FromBase64String(imageString);
            return ToImage(bytes, ThumbNailSieze);
        }
        public static string ImageToString(string file,  ILogger logger)
        {
            // return @"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";
            try
            {
                using (var image = PhotoManipulation.ResizeImage(ThumbNailSieze, file))
                {
                    using (var ms = new MemoryStream())
                    {
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        image.Dispose();
                        // image = null;
                        ms.Seek(0, SeekOrigin.Begin);
                        var bytes = ms.ToArray();
                        string stringVal = Convert.ToBase64String(bytes);

                        ms.Dispose();
                        // ms = null;
                        bytes = null;
                        return stringVal;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.SendDebugLogAsync($"Can't get thumbnail error: {ex.Message}. Consider deleting this file: {file}");
                return string.Empty;
            }
        }
        public static BitmapImage ToImage(byte[] array, int width)
        {
            using (var ms = new System.IO.MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.DecodePixelWidth = width;
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
        public static ImageSource GetImageNewFromFile(string fileName, int thumbnailWitdh = 0)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }
            Rotation rotate = DetermineOrientation(fileName);
            using (Stream stream = PhotoManipulation.FileToStream(fileName))
            {
                var bitmap = new BitmapImage();
                try
                {
                    bitmap.BeginInit();
                    bitmap.DecodePixelWidth = thumbnailWitdh;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    if (rotate != Rotation.Rotate0)
                    {
                        bitmap.Rotation = rotate;
                    }
                    bitmap.EndInit();
                    return bitmap;
                }
                catch (Exception)
                {
                    // _logger.SendLog($"Error loading image: {fileName}. Error: {ex.Message}");
                    return null;
                }
                finally
                {
                    GC.Collect();
                }
            }
        }
        public static ImageSource GetImageNew(string fileName, int thumbnailWitdh = 0)
        {
            try
            {
                Rotation rotate = DetermineOrientation(fileName);
                using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    stream.Seek(0, SeekOrigin.Begin);

                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.DecodePixelWidth = thumbnailWitdh;

                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = stream;

                    if (rotate != Rotation.Rotate0)
                    {
                        bi.Rotation = rotate;
                    }
                    bi.EndInit();
                    bi.Freeze();
                    stream.Close();
                    stream.Dispose();
                    return bi;
                }

            }
            catch (Exception)
            {
                // _logger.SendDebugLog($"Error loading image: {fileName}. Error: {ex.Message}");
                return null;
            }
        }
        private static Rotation DetermineOrientation(string file)
        {
            try
            {
                using (System.Drawing.Image img = System.Drawing.Image.FromFile(file))
                {
                    foreach (var prop in img.PropertyItems)
                    {
                        if ((prop.Id == 0x0112 || prop.Id == 5029 || prop.Id == 274))
                        {
                            var value = (int)prop.Value[0];
                            if (value == 6)
                            {
                                return Rotation.Rotate90;
                            }
                            else if (value == 3)
                            {
                                return Rotation.Rotate180;
                            }
                            else if (value == 8)
                            {
                                return Rotation.Rotate270;
                            }
                        }
                    }
                }
                return Rotation.Rotate0;
            }
            catch (Exception)
            {
                return Rotation.Rotate0;
            }
        }

        public string ThumbNail
        {
            get { return thumbNail; }
            set
            {
                if (this.thumbNail != value)
                {
                    this.thumbNail = value;
                    base.NotifyChanged("ThumbNail");
                }
            }
        }
        private string thumbNail;
        public string FullPath
        {
            get { return fullPath; }
            set
            {
                if (this.fullPath != value)
                {
                    this.fullPath = value;
                    base.NotifyChanged("FullPath");
                }
            }
        }
        private string fullPath;
        public Photo()
        {
        }
        public Photo(string path)
        {
            FullPath = path;
        }
        public void Clear()
        {
            _image = null;
        }
    }
}
