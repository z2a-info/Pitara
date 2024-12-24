using CommonProject.Src.Cache;
using ControllerProject.Src;
using Lucene.Net.Search;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace CommonProject.Src.Views
{
    public class RecommendationView: BaseView
    {
        private AppSettings _appSettings;
        private Dictionary<string, string> SampleQueries;
        private static int Maxrecommendations = 12;
        private static ILogger _logger;
        private ILuceneService _luceneService;
        private LocationCache _locationCache;
        public RecommendationView(LocationCache locationCache, ILogger logger, ILuceneService luceneService, AppSettings appSettings)
            : base(locationCache, logger, nameof(MyTagView), luceneService)
        {
            _logger = logger;
            _luceneService = luceneService;
            _appSettings = appSettings;
            _locationCache = locationCache;
            SampleQueries = InitializeSamepleQueries();
        }
        public override async Task ComputeRecommendations(BaseThreadSafeFileCache<string> XlocationCache)
        {
            try
            {
                // _recommendationList.Clear();
                DateTime dateTime = DateTime.Today;
                LocationView locationView = new LocationView(_locationCache,
                    _logger,
                    _luceneService,
                    _appSettings);

                var dict = await locationView.GetTopDozenVisits();
                int i = 0;
                foreach (var keyval in dict)
                {
                    switch (i)
                    {
                        case 0:
                            {
                                var key = $"On Weekend in {keyval.Key}";
                                var val = $"weekend {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 1:
                            {
                                var key = $"At Afternoon in {keyval.Key}";
                                var val = $"afternoon {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 2:
                            {
                                var key = $"At Morning in {keyval.Key}";
                                var val = $"morning {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 3:
                            {
                                var key = $"At Nights in {keyval.Key}";
                                var val = $"night {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 4:
                            {
                                var key = $"At Evening in {keyval.Key}";
                                var val = $"Evening {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 5:
                            {
                                var key = $"At Midnight in {keyval.Key}";
                                var val = $"midnight {keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 6:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 7:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 8:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 9:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 10:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        case 11:
                            {
                                var key = $"In {keyval.Key}";
                                var val = $"{keyval.Value}";
                                if (!SampleQueries.ContainsKey(key))
                                {
                                    SampleQueries.Add(key, val);
                                }
                                break;
                            }
                        default:
                            {
                                continue;
                            }
                    }
                    i++;
                }
                //foreach (var query in SampleQueries)
                //{
                //    // _logger.SendLogAsync($"Sample Query : {query.Key} -> {query.Value}");
                //    NLPSearchProcessor nlp = new NLPSearchProcessor(query.Value, _logger);
                //    string searchTermTranslated = nlp.GetTranslatedSearchTerm(query.Value);
                //    if(string.IsNullOrEmpty(searchTermTranslated))
                //    {
                //        _logger.SendLogAsync($"Error: Problematic : {query.Value}");
                //    }
                //}

                foreach (var query in SampleQueries)
                {
                    // logger.SendLog($"Query keyval -> {query.Key},{query.Value}");
                    var info = await _luceneService.GetQueryInfoAsync(query.Key, query.Value, false);
                    if (info.ResultCount > 0)
                    {
                        if (!_recommendationList.ContainsKey(info.QueryName))
                        {
                            _recommendationList.Add(info.QueryName, info);
                        }
                        if (_recommendationList.Count == Maxrecommendations)
                        {
                            break;
                        }
                    }
                    else
                    {
                        // _logger.SendDebugLogAsync($"No results --> {query.Key},{query.Value}, {info.ResultCount}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.SendLogAsync($"ComputeRecommendations (recommendation) error: {ex.Message}");
                _logger.SendLogWithException("Details - ", ex);
            }
        }

        private Dictionary<string, string> InitializeSamepleQueries()
        {
            DateTime dateTime = DateTime.Today;
            string todasDate = Utils.DayString(dateTime.Day);

            string strMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTime.Month);
            string strLastMonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTime.AddMonths(-1).Month);

            string lastThreeMonths = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTime.Month)} OR {strLastMonthName} OR {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dateTime.AddMonths(-2).Month)}";

            string year = dateTime.Year.ToString();

            string lastYear = $"{dateTime.Year - 1}";
            //string lastFiveYears = "last5year";
            //string lastTenYears = "last10year";
            Dictionary<string, string> SampleQueries = new Dictionary<string, string>()
            {
                ["50 years of today"] = $"from 50 years in {strMonthName} on {todasDate}", //1
                ["Last few Christmas"] = $"december 25th from five years", //1
                ["Decades of 4th July"] = $"july 4th from 10 years", //4
                ["Recent Fridays nights"] = $"friday night {year} ({strMonthName} or {strLastMonthName})", //2
                ["Last few New-Year"] = $"january {Utils.DayString(1)} from five years", //3
                ["Last Fall's Saturdays"] = $"Fall saturday {(dateTime.Year - 1).ToString()}", //6
                ["Last three Halloweens"] = $"October 31st from three years", //7
                ["Winter Friday nights of 2002"] = $"Winter on friday night 2012", //12
                ["Winter Friday nights of last 5 years"] = $"Winter on friday night from 5 years", //12
                ["Summer Saturday afternoon of last 10 years"] = $"Summer on Saturday afternoon from 10 years", //12
                ["Last year's Spring"] = $"Spring {lastYear}", //12
                ["Summer Saturdays 8 to 10pm"] = $"summer saturday night (8pm or 9pm or 10pm)", //13
                ["5 years ago this month"] = $"from {(dateTime.Year - 5).ToString()} {strMonthName}", //14
                ["1 years ago today"] = $"from {(dateTime.Year - 1).ToString()} {strMonthName} {todasDate}", //14
                ["2 years ago today"] = $"from {(dateTime.Year - 2).ToString()} {strMonthName} {todasDate}", //14
                ["3 years ago today"] = $"from {(dateTime.Year - 3).ToString()} {strMonthName} {todasDate}", //14
                ["4 years ago today"] = $"from {(dateTime.Year - 4).ToString()} {strMonthName} {todasDate}", //14
                ["5 years ago today"] = $"from {(dateTime.Year - 5).ToString()} {strMonthName} {todasDate}", //14
                ["All Monday nights of this year"] = $"from {(dateTime.Year).ToString()} monday night", //15
                ["Last few Wednesday afternoon"] = $"wednesday afternoon {year} ({strMonthName} or {strLastMonthName})",
                ["Decades of Monday 6 to 7 pm"] = $"monday (6pm OR 7pm) from 10 years", //4
            };

            return SampleQueries;
        }
    }
}
