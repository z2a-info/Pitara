using ControllerProject.Src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonProject.Src
{
    public static class NLPPatterns
    {
        public static string From_Range_2002_2004 = @"\s*(from)\s+\d{4}-\d{4}";
        
        // public static string From_Range_2002_2004 = @"^\d{4}-\d{4}";

        public static string From_10 = @"\s*(from)\s+\d{1,2}\s*";
        // public static string From_10 = @"^\d{2}";

        public static string From_2004 = @"\s*(from)\s+\d{4}\s*";
        public static string Just_2004 = @"^\d{4}";

        public static string From_10_Years = @"\s*(from)\s+\d{1,2}\s+(years|year|months|month)+\s*";
        // public static string From_10_Years =  @"\s*\d{1,2}\s+(years|year|months|month)+\s*";
        
        public static string From_Ten_Years = @"\s*(from)\s+("+GetNumberWords()+@")\s+(years|year|months|month)+\s*";
        // public static string From_Ten_Years = @"\s*("+GetNumberWords()+@")\s+(years|year|months|month)+\s*";

        public static string Feet_or_Feets = @"\s*\d{1,2}(kfeets|kfeet)\s*";
        public static string Feet_Plus = @"\s*\d{1,2}(kfeetplus)\s*";

        public static string GetNumberWords()
        {
            return string.Join("|", NLPSearchProcessor.NumberStrings.Keys);
        }
    }
}
