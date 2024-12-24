using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CommonProject.Src
{
    public static class TagsHelper
    {
        public static string UppercaseFirst(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return char.ToUpper(s[0]) + s.Substring(1);
        }
        public static string BreakCameCaseWords(string input)
        {
            // Break input into multiple words before breaking them further from camel case.
            string[] separatewords = input.Split(new char[] { ' ' });
            StringBuilder sb = new StringBuilder();
            foreach (var item in separatewords)
            {
                string[] words = Regex.Matches(item, "(^[a-z]+|[A-Z]+(?![a-z])|[A-Z][a-z]+)")
                    .OfType<Match>()
                    .Select(m => m.Value)
                    .ToArray();
                sb.Append(" " + string.Join(" ", words));
            }
            return sb.ToString();
        }
        public static string[] StringToWords(string stringTags)
        {
            var words = stringTags.Split(new char[] { ' ', ',', ';' },StringSplitOptions.RemoveEmptyEntries).ToList();
            var cleanedup = words.
                Select(x=> x)
                .Distinct()
                .OrderBy(x=> x.Length);
            return cleanedup.ToArray();
        }
        public static string WordsToString(string[] words)
        {
            return string.Join(" ", words);
        }
        public static bool IsAnyMissingInTarget(string targetTag, string sourceTags)
        {
            var srcWords = StringToWords(sourceTags);
            var targetWords = StringToWords(targetTag);
            foreach (var item in srcWords)
            {
                if(targetWords.Contains(item))
                {
                    continue;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
        public static bool IsAnyPresentInTarget(string targetTag, string sourceTags)
        {
            var srcWords = StringToWords(sourceTags);
            var targetWords = StringToWords(targetTag);
            foreach (var item in srcWords)
            {
                if (targetWords.Contains(item))
                {
                    return true;
                }
                else
                {
                    continue;
                }
            }
            return false;
        }
        public static string SanitizeSearchTerm(string queryString)
        {
            string[] keywordsArray = queryString.Split(new char[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return String.Join(" ", keywordsArray);
        }

        public static string NormalizeTAGs(string tagsInput, bool smartRemove, bool removeInsignificant, int removeSmallerThanThisLength, bool changeToTitleCaseEachWord, bool sort, int limit)
        {
            // tagsInput = tagsInput.ToLower();
            string[] insignificantWords = new string[]
            {
                " and ",
                " the ",
                " that ",
                " this ",
                " and ",
                " or ",
                " for ",
                " while ",
                " far ",
                " which ",
                " she ",
                " he ",
                " you ",
                " to "
            };

            if (removeInsignificant == true)
            {
                foreach (string word in insignificantWords)
                {
                    tagsInput = tagsInput.Replace(word, " ");
                }
            }

            tagsInput = tagsInput.Replace('\n', ' ');
            tagsInput = tagsInput.Replace(';', ' ');
            tagsInput = tagsInput.Replace(',', ' ');
            tagsInput = tagsInput.Replace('\0', ' ');
            tagsInput = tagsInput.Replace('\\', ' ');
            tagsInput = tagsInput.Replace('/', ' ');
            tagsInput = tagsInput.Replace(':', ' ');
            tagsInput = tagsInput.Replace('-', ' ');
            tagsInput = tagsInput.Replace('_', ' ');
            tagsInput = tagsInput.Replace('@', ' ');

            string[] keywordsArray = tagsInput.Split(new char[] { ' ' });

            keywordsArray = keywordsArray.Where(x => !string.IsNullOrEmpty(x) && x.Length >= removeSmallerThanThisLength).ToArray();
            keywordsArray = keywordsArray.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
            keywordsArray = keywordsArray.Select(x => x.Trim()).ToArray();
            if (true == sort)
            {
                Array.Sort(keywordsArray);
            }

            string outString = string.Empty;
            int count = 0;
            foreach (string keyword in keywordsArray)
            {
                if (smartRemove == true)
                {
                    //if (IsGoodTag(keyword) == false)
                    //{
                    //    // Let's not pick the ones which is bad.
                    //    continue;
                    //}
                }
                if (changeToTitleCaseEachWord == true)
                {
                    outString += UppercaseFirst(keyword.ToLower().Trim());
                }
                else
                {
                    outString += keyword.Trim();
                }
                outString += " ";
                count++;
                if (limit != -1 && count == limit)
                {
                    break;
                }
            }
            outString = outString.Trim(' ').Trim(',');
            return outString;
        }

        internal static string FilterNumericTags(string input)
        {
            string[] tags = input.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            var filtered = tags.Where(x => !Utils.DoesContainsNumbers(x));
            return string.Join(" ", filtered.ToArray());
        }
    }
}
