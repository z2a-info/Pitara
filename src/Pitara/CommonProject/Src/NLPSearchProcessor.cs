using CommonProject.Src;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace ControllerProject.Src
{
    public class NLPSearchProcessor
    {
        private string[] _allowedKeywords = new string[]
            {
                "from",
                "feetplus",
                "feet"
            }.ToArray();

        public static Dictionary<string, string> _purals = new Dictionary<string, string>
            {
                { "mornings", "morning"},
                { "noons", "noon"},
                { "afternoons", "afternoon"},
                { "evenings", "evening"},
                { "nights", "night"},

                { "weekdays", "weekday"},
                { "weekends", "weekend"},

                { "sundays", "sunday"},
                { "mondays", "monday"},
                { "tuesdays", "tuesday"},
                { "wednesdays", "wednesday"},
                { "thursdays", "thursday"},
                { "fridays", "friday"},
                { "saturdays", "saturday"},
            };

        public static Dictionary<string, int> NumberStrings = new Dictionary<string, int>
            {
                { "one", 1},
                { "two", 2},
                { "three", 3},
                { "four", 4},
                { "five", 5},
                { "six", 6},
                { "seven", 7},
                { "eight", 8},
                { "nine", 9},
                { "ten", 10},
                { "eleven", 11},
                { "twelve", 12},
                { "thirteen", 13},
                { "fourteen", 14},
                { "fifteen", 15},
                { "sixteen", 16},
                { "seventeen", 17},
            };



        //public static Dictionary<string, string> _HelperWordsMap = new Dictionary<string, string>
        //    {
        //        { "week1", $"{Utils.GetWeekNQueryFragment(1)}"},
        //        { "week2", $"{Utils.GetWeekNQueryFragment(2)}"},
        //        { "week3", $"{Utils.GetWeekNQueryFragment(3)}"},
        //        { "week4", $"{Utils.GetWeekNQueryFragment(4)}"},
        //        { "week5", $"{Utils.GetWeekNQueryFragment(5)}"},

        //        //{ "last6month", $"{Utils.GetLastNMonthsQueryFragment(6)}"},

        //        //{ "lastyear", $"{Utils.GetLastNYearsQueryFragment(1)}"},
        //        //{ "last5year", $"{Utils.GetLastNYearsQueryFragment(5)}"},
        //        //{ "last10year", $"{Utils.GetLastNYearsQueryFragment(10)}"},
        //        //{ "last15year", $"{Utils.GetLastNYearsQueryFragment(15)}"},
        //        //{ "last20year", $"{Utils.GetLastNYearsQueryFragment(20)}"},

        //        { "1kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(1)}"},
        //        { "2kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(2)}"},
        //        { "3kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(3)}"},
        //        { "4kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(3)}"},
        //        { "5kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(5)}"},
        //        { "6kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(5)}"},
        //        { "7kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(7)}"},
        //        { "8kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(7)}"},
        //        { "9kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(7)}"},
        //        { "10kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(10)}"},

        //        { "15kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(15)}"},
        //        { "20kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(20)}"},
        //        { "25kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(25)}"},
        //        { "30kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(30)}"},
        //        { "35kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(35)}"},
        //        { "40kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(40)}"},
        //        { "45kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(45)}"},
        //        { "50kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(50)}"},
        //        { "55kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(55)}"},
        //        { "60kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(60)}"},
        //        { "65kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(65)}"},
        //        { "70kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(70)}"},
        //        { "75kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(75)}"},
        //        { "80kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(80)}"},
        //        { "85kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(85)}"},
        //        { "90kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(90)}"},
        //        { "95kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(95)}"},
        //        { "100kfeetplus", $"{Utils.GetHigherThanNKFeetQueryFragment(100)}"},
        //    };

        private string _sentence;
        private ILogger _logger;
        public NLPSearchProcessor(string sentence, ILogger logger)
        {
            _logger = logger;
            _sentence = sentence;
        }
        public string GetAutoCorrectedSearchTerm()
        {
            return _sentence;
        }
        public string GetSearchTerm()
        {
            return _sentence;
        }
        private string HandleKeywords(string searchTerm)
        {
            string updatedSearchTerm = string.Empty;
            // Process multiple ocurrences of the same keywords not just first one.
            bool feetPlusDone = false; // Only Feetplus or feet allowed both can't stay together.
            foreach (string keyword in _allowedKeywords)
            {
                var searchTermTemp = searchTerm;
                if(keyword == "from")
                {
                    updatedSearchTerm = HandleAllInstanceOf_From(searchTerm);
                }
                if (keyword == "feetplus")
                {
                    if(updatedSearchTerm.IndexOf(keyword)>=0)
                    {
                        updatedSearchTerm = HandleAllInstanceOf_FeetPlus(updatedSearchTerm);
                        feetPlusDone = true;
                    }
                }
                if (keyword == "feet" && feetPlusDone == false)
                {
                    updatedSearchTerm = HandleAllInstanceOf_Feet(updatedSearchTerm);
                }
            }
            return updatedSearchTerm.ToLower();
        }

        private string HandleAllInstanceOf_Feet(string searchTerm)
        {
            var match = NLPSearchProcessor.Match(searchTerm, NLPPatterns.Feet_or_Feets);
            if (match.Count > 0)
            {
                searchTerm = NLPSearchProcessor.Replace(searchTerm, NLPPatterns.Feet_or_Feets
                    , ExpandFrom_Feet(match[0].ToString().Trim()));
                return searchTerm;
            }
            return searchTerm;
        }
        private string HandleAllInstanceOf_FeetPlus(string searchTerm)
        {
            var match = NLPSearchProcessor.Match(searchTerm, NLPPatterns.Feet_Plus);
            if (match.Count > 0)
            {
                searchTerm = NLPSearchProcessor.Replace(searchTerm, NLPPatterns.Feet_Plus
                    , ExpandFrom_FeetPlus(match[0].ToString().Trim()));
                return searchTerm;
            }
            return searchTerm;
        }

        private SortedDictionary<int, string> MapKeywordLocations(string searchTerm)
        {
            SortedDictionary<int, string> keywordSequence = new SortedDictionary<int, string>();

            // Process multiple ocurrences of the same keywords not just first one.
            foreach (string keyword in _allowedKeywords)
            {
                var searchTermTemp = searchTerm;
                var pattern = $@"\b{keyword.Trim()}\b";
                var matches = Match(searchTermTemp, pattern);
                foreach(Match match in matches)
                {
                    keywordSequence.Add(match.Index, match.Value);
                }
            }
            keywordSequence.OrderBy(x => x.Key);
            return keywordSequence;
        }
        public string GetTranslatedSearchTerm(string searchTerm)
        {
            try 
            {
                //searchTerm = " " + searchTerm;

                //string[] parts = searchTerm.ToLower().Split(_allowedKeywords, StringSplitOptions.RemoveEmptyEntries);

                //string[] searchTermWords = searchTerm.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                //if (!StartWithKeyword(searchTermWords))
                //{
                //    _logger.SendLogAsync($"SearchTerm: doesnt start with keyword {searchTerm}");
                //    return string.Empty;
                //}

                // We know it start from one of the keyword. But what if its only keyword and nothing else.
                //if (searchTermWords.Length == 1)
                //{
                //    // _logger.SendLogAsync($"SearchTerm only keyword: {searchTerm}");
                //    return string.Empty;
                //}

                // var keywordSequence = MapKeywordLocations(searchTerm);
                // There is no keywords detected
                //if (keywordSequence.Count() == 0)
                //{
                //    _logger.SendLogAsync($"SearchTerm no keyword: {searchTerm}");
                //    return string.Empty;
                //}

                //List<KeyValuePair<string, string>> buckets = new List<KeyValuePair<string, string>>();
                //int i = 0;
                //foreach (int key in keywordSequence.Keys)
                //{
                //    // does sentence doesnt start with keyword
                //    if(i == 0 && (key> 0))
                //    {
                //        buckets.Add(new KeyValuePair<string, string>("<No-Keyword>", parts[i++]));
                //        buckets.Add(new KeyValuePair<string, string>(keywordSequence[key], parts[i++]));
                //    }
                //    else
                //    {
                //        buckets.Add(new KeyValuePair<string, string>(keywordSequence[key], parts[i++]));
                //    }
                //}

                // string finalSearchTerm;

                //if (buckets.Count> 0)
                //{
                //    finalSearchTerm = string.Empty;
                //    foreach (KeyValuePair<string, string> pair in buckets)
                //    {
                //        switch (pair.Key.Trim().ToLower())
                //        {
                //            case "from":
                //                {
                //                    finalSearchTerm += " " + HandleAllInstanceOf_From(pair.Value);
                //                    break;
                //                }
                //            default:
                //                {
                //                    Debug.Assert(false, "Unknown keyword");
                //                    break;
                //                }
                //        }
                //    }
                //}
                //else
                //{
                //    finalSearchTerm = searchTerm;
                //}

                string finalSearchTerm = HandleKeywords(searchTerm);

                finalSearchTerm = RemoveNoiseWords(" "+finalSearchTerm);
                finalSearchTerm = NormalizeSearchTerm(finalSearchTerm);
                // _logger.SendLogAsync($"Translated query: {finalSearchTerm}");

                // Utils.DisplayMessageBox(finalSearchTerm);
                return finalSearchTerm.Trim();
            }
            catch (Exception ex) 
            {
                _logger.SendDebugLogAsync($"Error processing search term: {searchTerm}. Error: {ex.Message}");
                return string.Empty;
            }
        }
        private string RemoveNoiseWords(string finalSearchTerm)
        {
            string[] ignoreWords = new string[] {
                "at",
                "in",
                "out",
                "on",
                "and",
                "of",
                "off",
                "to",
                "be",
                "the",
            }.Select(x => x = " " + x + " ").ToArray();

            foreach (string ignoreWord in ignoreWords)
            {
                finalSearchTerm = finalSearchTerm.Replace(ignoreWord, " ");
            }
            return finalSearchTerm.Trim();
        }

        private bool StartWithKeyword(string[] parts)
        {
            if (parts.Length > 0)
            {
                var keywordToCheck = " " + parts[0].ToLower().Trim() + " ";
                //find in the keywords if first word is present
                var result = Array.Find(_allowedKeywords, element => element == keywordToCheck);
                if (result == keywordToCheck)
                {
                    return true;
                }
            }
            return false;
        }

        private string NormalizeSearchTerm(string finalSearchTerm)
        {
            var parts = finalSearchTerm.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            parts = parts.Select(x => x.Trim()).ToArray();
            string[] finalParts = new string[parts.Length];
            int i = 0;
            foreach (var item in parts)
            {
                // If pure number then safe to assume its date
                if (Utils.IsEntirelyNumericString(item))
                {
                    int number = Int32.Parse(item);
                    if (number > 0 && number < 32)
                    {
                        finalParts[i] = Utils.DayString(number);
                    }
                    else
                    {
                        finalParts[i] = item;
                    }
                }
                else
                {
                    if (_purals.ContainsKey(item) == true)
                    {
                        finalParts[i] = _purals[item];
                    }
                    else
                    {
                        finalParts[i] = item;
                    }
                }
                i++;
            }
            return string.Join(" ", finalParts);
        }

        private static string Replace(string input, string pattern,  string replaceWith)
        {
            var result = Regex.Replace(input, pattern, replaceWith, RegexOptions.IgnoreCase);
            return result;
        }
        private static MatchCollection Match(string input, string pattern)
        {
            var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);
            return matches;
        }
        private string HandleAllInstanceOf_From(string fromString)
        {
            var match = NLPSearchProcessor.Match(fromString, NLPPatterns.From_10_Years);
            if (match.Count > 0)
            {
                fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.From_10_Years
                    , ExpandFrom_10_Years(match[0].ToString().Trim()));
                return fromString;
            }

            match = NLPSearchProcessor.Match(fromString, NLPPatterns.From_Ten_Years);
            if (match.Count > 0)
            {
                fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.From_Ten_Years
                    , ExpandFrom_Ten_Years(match[0].ToString().Trim()));
                return fromString;
            }

            //match = NLPSearchProcessor.Match(fromString, NLPPatterns.From_Range_2002_2004);
            //if (match.Count > 0)
            //{
            //    fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.From_Range_2002_2004
            //        , " " + match[0].ToString().Trim());
            //    return fromString;
            //}

            match = NLPSearchProcessor.Match(fromString, NLPPatterns.From_2004);
            if (match.Count > 0)
            {
                fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.From_2004
                    ,ExpandFromYear(match[0].ToString().Trim()));
                return fromString;
            }
            match = NLPSearchProcessor.Match(fromString, NLPPatterns.Just_2004);
            if (match.Count > 0)
            {
                fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.Just_2004
                    , ExpandFromJustYear(match[0].ToString().Trim()));
                return fromString;
            }
            

            match = NLPSearchProcessor.Match(fromString, NLPPatterns.From_10);
            if (match.Count > 0)
            {
                fromString = NLPSearchProcessor.Replace(fromString, NLPPatterns.From_10
                    , ExpandFrom_10_Years(match[0].ToString().Trim()+ " years"));
                return fromString;
            }
            // Assuming its folder name from garbage
            return fromString;
        }

        private string ExpandFrom_Ten_Years(string fromTerm)
        {
            string[] parts = fromTerm.Split(new char[] { ' ' });
            int count = NumberStrings[parts[1].Trim()];
            string yearMonth = parts[2].Trim();

            return ExpandFrom_N_YearMonths(count, yearMonth);
        }

        private string ExpandFromJustYear(string fromTerm)
        {
            return " " + fromTerm + " ";
        }
        private string ExpandFromYear(string fromTerm)
        {
            string[] parts = fromTerm.Split(new char[] { ' ' });
            return " " + parts[1].Trim() + " ";
        }

        private string ExpandFrom_Feet(string fromTerm)
        {
            return " "+fromTerm.TrimEnd('s')+ " ";
        }

        private string ExpandFrom_FeetPlus(string fromTerm)
        {
            //9kfeetplus
            var number = fromTerm.Replace("kfeetplus", string.Empty);
            int num = 0;
            if(int.TryParse(number.Trim(), out num))
            {
                var results =  " " + $"{Utils.GetHigherThanNKFeetQueryFragment(num)}" + " ";
                return results;
            }

            return fromTerm;
        }
        

        private string ExpandFrom_10_Years(string fromTerm)
        {
            string[] parts = fromTerm.Split(new char[] { ' ' });
            int count = Int32.Parse(parts[1].Trim());
            string yearMonth = parts[2].Trim();

            return ExpandFrom_N_YearMonths(count, yearMonth);
        }

        private string ExpandFrom_N_YearMonths(int count, string yearMonth)
        {
            var year = DateTime.Now.Year;

            switch (yearMonth.Trim())
            {
                case "years":
                case "year":
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(" ( ");
                        sb.Append($"{year - 1}");

                        for (int i = 2; i <= count; i++)
                        {
                            sb.Append($" or {year - i}");
                        }
                        sb.Append(" ) ");
                        return sb.ToString();
                    }
                case "months":
                case "month":
                    {
                        var month = DateTime.Now.Month;
                        month = month - 1;
                        StringBuilder sb = new StringBuilder();
                        sb.Append(" ( ");
                        while (month > 1 && count > 1)
                        {
                            sb.Append($"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month--).ToLower()} or ");
                            count--;
                        }
                        sb.Append($"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month--).ToLower()}");
                        sb.Append($" ) {year} ");
                        return sb.ToString();
                    }
                default:
                    {
                        throw new Exception("Only year and month supported");
                    }
            }
        }
    }
}
