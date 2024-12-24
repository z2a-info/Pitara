using CommonProject.Src;
using ControllerProject.Src;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System;
using System.Text.RegularExpressions;

namespace Tests
{
    [TestClass]
    public class TestNLPProcessor
    {
        CommonProject.Src.ILogger _logger = new AsyncLog(System.IO.Directory.GetCurrentDirectory()+@"\", true);

        [TestCleanup]
        public async Task Cleanup()
        {
            await Task.Delay(500);
        }

        [TestMethod]
        [DataRow("in afternoon at sammamish", "afternoon sammamish")]
        [DataRow("in the afternoon at the sammamish", "afternoon sammamish")]
        [DataRow("from 2002 at sammamish", "2002 sammamish")]
        [DataRow("from 2002 in January at sammamish", "2002 january sammamish")]
        [DataRow("from 2002 in January at sammamish on Sunday", "2002 january sammamish sunday")]
        [DataRow("January sammamish", "january sammamish")] 
        [DataRow("in the summer", "summer")] 
        [DataRow("on the summer", "summer")] 
        [DataRow("foo", "foo")] // No keywords
        [DataRow("at morning at sammamish", "morning sammamish")] // No keywords
        [DataRow("from 2003 Garbage at sammamish", "2003 garbage sammamish")]
        [DataRow("from 2003 in January on sunday at 7:30am ", "2003 january sunday 7:30am")]
        [DataRow("one two", "one two")]
        // Or
        [DataRow("one or two", "one or two")]

        [DataRow("from 5 years one", "( 2022 or 2021 or 2020 or 2019 or 2018 ) one")]
        // [DataRow("from 3 year in Sepember week1 in morning on weekday", "( 2022 or 2021 or 2020 ) sepember (1st or 2nd or 3rd or 4th or 5th or 6th or 7th) morning weekday")]
        [DataRow("from 4 months in morning on weekday", "( august or july or june or may ) 2023 morning weekday")]
        [DataRow("from 20 months in morning", "( august or july or june or may or april or march or february or january ) 2023 morning")]
        [DataRow("from 4 month in morning on weekday", "( august or july or june or may ) 2023 morning weekday")]
        [DataRow("from 5 years morning", "( 2022 or 2021 or 2020 or 2019 or 2018 ) morning")]
        [DataRow("at badrinath from 5 years", "badrinath ( 2022 or 2021 or 2020 or 2019 or 2018 )")]
        [DataRow("from 2004", "2004")]
        [DataRow("from 20", "( 2022 or 2021 or 2020 or 2019 or 2018 or 2017 or 2016 or 2015 or 2014 or 2013 or 2012 or 2011 or 2010 or 2009 or 2008 or 2007 or 2006 or 2005 or 2004 or 2003 )")]
        [DataRow("from 10 in issaquah in june on weekday in morning", "( 2022 or 2021 or 2020 or 2019 or 2018 or 2017 or 2016 or 2015 or 2014 or 2013 ) issaquah june weekday morning")]
        [DataRow("in afternoon at sammamish", "afternoon sammamish")]
        [DataRow("from 3 years in april weekend night first week", "( 2022 or 2021 or 2020 ) april weekend night first week")]
        [DataRow("delhi iphone from three years april weekend night  first week three days", "delhi iphone ( 2022 or 2021 or 2020 ) april weekend night first week three days")]
        [DataRow("2002 in january at sammamish", "2002 january sammamish")]
        [DataRow("at badrinath from 5 years", "badrinath ( 2022 or 2021 or 2020 or 2019 or 2018 )")]
        [DataRow("from 2002 9kfeets", "2002 9kfeet")]
        [DataRow("sammamish 19kfeets", "sammamish 19kfeet")]
        [DataRow("from 2 years 19kfeet sammamish", "( 2022 or 2021 ) 19kfeet sammamish")]
        [DataRow("from 2 years 9kfeets sammamish", "( 2022 or 2021 ) 9kfeet sammamish")]
        [DataRow("from 2 years 95kfeetplus sammamish", "( 2022 or 2021 ) ( 95kfeet or 96kfeet or 97kfeet or 98kfeet or 99kfeet or 100kfeet ) sammamish")]

        public void Helper(string input, string expected)
        {
            NLPSearchProcessor nlp = new NLPSearchProcessor(input, _logger);
            var autocorrected = nlp.GetAutoCorrectedSearchTerm();
            var translated = nlp.GetTranslatedSearchTerm(autocorrected);
            Assert.AreEqual(expected, translated);

        }
        [TestMethod]

        [DataRow("from 2 years 9 january sammamish", "( 2023 or 2022 ) 9th january sammamish")]
        [DataRow("22 march", "22nd march")]
        [DataRow("19 January", "19th january")]

        public void TestOne(string input, string expected)
        {
            Helper(input, expected);
        }

        [TestMethod]
        public void RegExTest()
        {
            // string pattern = "(Mr\\.? |Mrs\\.? |Miss |Ms\\.? )";
            string pattern = string.Empty;
            
            pattern += @"^\d{4}-\d{4} |"; // 2002-2004
            // Working
            pattern += @"\d{1,2}\s+([year|month|week])+\b*"; // 10 years/year
            

            string input = "aray 2                weeks in January on Weekend";
            var matches = Regex.Matches(input, pattern, RegexOptions.IgnoreCase);

        }
    }
}