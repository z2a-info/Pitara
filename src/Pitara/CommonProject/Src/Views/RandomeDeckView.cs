using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
// using Pitara.PhotoStuff;
// using Pitara.ViewModel;
// using PitaraLuceneSearch.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Effects;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace CommonProject.Src.Views
{
    public class RandomeDeckView
    {
        private static int _maxrecommendations = 200;
        private static ILogger _logger;
        private ILuceneService _luceneService;
        private List<Photo> _recommendation = new List<Photo>();
        private UserSettings _userSettings;
        public RandomeDeckView(ILogger logger, ILuceneService luceneService, UserSettings userSettings)
        {
            _userSettings = userSettings;
            _logger = logger;
            _luceneService = luceneService;
        }
        private List<int> GetRandomeIndexes(int maxIndex, int count)
        {
            int Min = 0;
            int Max = maxIndex;
            Random randNum = new Random();
            var result = Enumerable
                .Repeat(0, count)
                .Select(i => randNum.Next(Min, Max))
                .ToList();

            // Let's only return unique indexes
            HashSet<int> uniqueIndexes = new HashSet<int>();
            foreach(var item in result)
            {
                if(!uniqueIndexes.Contains(item))
                {
                    uniqueIndexes.Add(item);
                }
            }
            return uniqueIndexes.ToList();
        }
        //public void Reset()
        //{
        //    _recommendation.Clear();
        //}
        public async Task Reset()
        {
            await Task.Run(()=> _recommendation.Clear());
        }

        private async Task ComputeRecommendations()
        {
            _recommendation.Clear();
            await Task.Run(()=>
            {
                var totalIndexed = _luceneService.GetIndexedPhotoCount();
                int maxIndex = (totalIndexed >= _maxrecommendations) ? _maxrecommendations : totalIndexed;
                List<int> randomList = GetRandomeIndexes(totalIndexed, maxIndex);
                using (FSDirectory fs = FSDirectory.Open(_userSettings.IndexFolder))
                {
                    if (IndexReader.IndexExists(fs))
                    {
                        using (IndexReader reader = IndexReader.Open(fs, true))
                        {
                            string previousHeading = string.Empty;
                            foreach (var item in randomList)
                            {
                                if (item > totalIndexed - 1)
                                {
                                    continue;
                                }
                                if (reader.IsDeleted(item))
                                {
                                    continue;
                                }
                                Document doc = reader.Document(item);
                                if (doc.Get("ThumbNail") == null)
                                {
                                    continue;
                                }
                                DisplayItem displayItem = new DisplayItem(doc);

                                if (displayItem.Heading.Equals(previousHeading))
                                {
                                    previousHeading = displayItem.Heading;
                                    displayItem.Heading = String.Empty;
                                }
                                else
                                {
                                    previousHeading = displayItem.Heading;
                                }

                                _recommendation.Add(new Photo()
                                {
                                    FullPath = displayItem.FilePath,
                                    ThumbNail = displayItem.ThumbNail,
                                    ToolTips = PhotoManipulation.FormatToolTip(displayItem.KeyWords, displayItem.Location),
                                    HeaderBackground = DisplayItem.Background,
                                    Heading = displayItem.Heading.ToString(),
                                });
                                previousHeading = displayItem.Heading;
                            }
                        }
                    }
                }

            });
        }
        public async Task<List<Photo>> GetTopRecommendationsYearAsync()
        {
            try
            {
                await ComputeRecommendations();
                return _recommendation;
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"Surprise  GetTopRecommendationsYearAsync. Error:{ex.Message}");
                return _recommendation;
            }
        }

    }
}