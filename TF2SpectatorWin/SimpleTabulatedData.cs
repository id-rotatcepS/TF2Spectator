using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TF2SpectatorWin
{
    // adapted and heavily simplified from https://stackoverflow.com/a/5436437 
    /// <summary>
    /// Convert tabulated values (tab-separated) into cells of strings. 
    /// No quoting or quote-escaping supported.
    /// </summary>
    public class SimpleTabulatedData
    {
        public SimpleTabulatedData()
        {
        }

        public List<string[]> ParseRows(string tsvText)
        {
            string[] lines = tsvText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            return ParseString(lines);
        }

        private List<string[]> ParseString(string[] lines)
        {
            List<string[]> result = new List<string[]>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                result.Add(ParseLineTabSeparated(line));
            }

            return result;
        }

        private string[] ParseLineTabSeparated(string line)
        {
            MatchCollection matchesTab = Regex.Matches(line, @"[\r\n\f\v ]*?((?<x>(?=[\t]+))|(?<x>[^\t]+))\t?",
                 RegexOptions.ExplicitCapture);

            string[] values = (from Match m in matchesTab
                               select m.Groups["x"].Value.Trim()
                               ).ToArray();

            return values;
        }

    }

}