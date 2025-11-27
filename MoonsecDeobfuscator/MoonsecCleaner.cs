using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MoonsecDeobfuscator
{
    public static class MoonsecCleaner
    {
        public static string Clean(string code)
        {
            code = CleanGarbage(code);
            var aliases = ExtractAliases(code);
            code = FixSyntax(code);
            code = RestoreMethodPattern(code, aliases);
            code = FixMethodCalls(code, aliases);
            code = StripEmptyLines(code);
            return code;
        }

        private static string CleanGarbage(string code)
        {
            var output = new List<string>();
            foreach (var rawLine in code.Split('\n'))
            {
                var line = rawLine.TrimStart();
                if (Regex.IsMatch(line, @"^goto\s+label_\d+") ||
                    Regex.IsMatch(line, @"^::label_\d+::$") ||
                    Regex.IsMatch(line, @"^--+\s*block#\d+") ||
                    Regex.IsMatch(line, @"^--+\s*visited") ||
                    Regex.IsMatch(line, @"^--+\s*line:\s*\[") ||
                    Regex.IsMatch(line, @"^--+\s*filename") ||
                    Regex.IsMatch(line, @"^--+\s*version"))
                {
                    continue;
                }
                output.Add(rawLine);
            }
            return string.Join("\n", output);
        }

        private static Dictionary<string, string> ExtractAliases(string code)
        {
            var dict = new Dictionary<string, string>();
            var regex = new Regex(@"(\w+)\s*=\s*""([A-Za-z_][A-Za-z0-9_]*)""");

            foreach (Match m in regex.Matches(code))
            {
                dict[m.Groups[1].Value] = m.Groups[2].Value;
            }

            return dict;
        }

        private static string FixSyntax(string code)
        {
            code = Regex.Replace(code, @"([\w_]+)\s*:\s*\[(.*?)\]", "$1[$2]");
            code = code.Replace("Connectlocal function", "Connect(function");
            code = Regex.Replace(code, @"\)(\s*)local\s+function", ")\nlocal function");
            return code;
        }


        private static string RestoreMethodPattern(string code, Dictionary<string, string> aliases)
        {
            var lines = new List<string>(code.Split('\n'));
            var result = new List<string>();

            int i = 0;
            while (i < lines.Count)
            {
                var line = lines[i];
                bool processed = false;
                var m = Regex.Match(line, @"^\s*(?:local\s+)?(\w+)\s*=\s*""([\w_]+)""");
                if (m.Success)
                {
                    string varName = m.Groups[1].Value;
                    string methodName = m.Groups[2].Value;
                    for (int j = i + 1; j <= i + 5 && j < lines.Count; j++)
                    {
                        var funcLine = lines[j];
                        if (Regex.IsMatch(funcLine, @"^\s*function\s+" + varName + @"\s*\((.*?)\)"))
                        {
                            var funcBlock = new List<string> { funcLine };
                            int depth = 1;
                            int k = j + 1;

                            while (k < lines.Count && depth > 0)
                            {
                                string body = lines[k];
                                funcBlock.Add(body);

                                if (Regex.IsMatch(body, @"\bfunction\b")) depth++;
                                if (Regex.IsMatch(body, @"\bend\b")) depth--;

                                k++;
                            }
                            for (int c = k; c < Math.Min(k + 10, lines.Count); c++)
                            {
                                string callLine = lines[c];
                                string pattern = @"^\s*([\w_\.]+)\[" + varName + @"\]\s*\(" + varName + @"\)";

                                var callMatch = Regex.Match(callLine, pattern);
                                if (callMatch.Success)
                                {
                                    string obj = callMatch.Groups[1].Value;

                                    string funcBody = string.Join("\n", funcBlock);
                                    funcBody = Regex.Replace(funcBody,
                                        @"^\s*function\s+" + varName + @"\s*\((.*?)\)",
                                        @"function($1)");

                                    result.Add($"{obj}:{methodName}({funcBody})");

                                    i = c;
                                    processed = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!processed)
                    result.Add(line);

                i++;
            }

            return string.Join("\n", result);
        }

        private static string FixMethodCalls(string code, Dictionary<string, string> aliases)
        {
            return Regex.Replace(code,
                @"([\w_\.]+)\[([\w_]+)\]\s*\((.*?)\)",
                match =>
                {
                    string obj = match.Groups[1].Value;
                    string alias = match.Groups[2].Value;
                    string args = match.Groups[3].Value;

                    return aliases.TryGetValue(alias, out string method)
                        ? $"{obj}:{method}({args})"
                        : match.Value;
                });
        }

        private static string StripEmptyLines(string code)
        {
            var result = new List<string>();
            bool lastBlank = false;

            foreach (var line in code.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!lastBlank)
                    {
                        result.Add("");
                        lastBlank = true;
                    }
                }
                else
                {
                    result.Add(line);
                    lastBlank = false;
                }
            }

            return string.Join("\n", result);
        }
    }
}
