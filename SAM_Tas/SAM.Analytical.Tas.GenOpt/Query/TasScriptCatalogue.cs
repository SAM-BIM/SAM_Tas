// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>The catalogue source of names found by <see cref="TasScriptCatalogue"/>.</summary>
        public const string TasScriptCatalogueSource = "Tas script";

        private static readonly Regex regex_Variable = new Regex("(?<!\\w)Variables\\s*\\[\\s*\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"\\s*\\]", RegexOptions.CultureInvariant);
        private static readonly Regex regex_Output = new Regex("(?<!\\w)ScriptOutput\\s*\\.\\s*SetValue\\s*\\(\\s*\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"", RegexOptions.CultureInvariant);

        /// <summary>
        /// The design variables a Tas script reads and the outputs it writes, found by a literal scan of the script text:
        /// <c>Variables["name"]</c> and <c>ScriptOutput.SetValue("name", …)</c>. Names are listed once, in the order they
        /// first appear; names inside <c>//</c> and <c>/* */</c> comments are ignored. A name built at run time (not a
        /// string literal) cannot be found. Nothing about units or ranges is known, so entries carry names only.
        /// </summary>
        /// <param name="scriptText">The text of the Tas script; null or empty gives an empty catalogue.</param>
        public static OptimisationCatalogue TasScriptCatalogue(string scriptText)
        {
            string text = WithoutComments(scriptText ?? string.Empty);

            return new OptimisationCatalogue(Entries(regex_Variable, text), Entries(regex_Output, text), TasScriptCatalogueSource);
        }

        private static List<OptimisationCatalogueEntry> Entries(Regex regex, string text)
        {
            List<OptimisationCatalogueEntry> result = new List<OptimisationCatalogueEntry>();
            HashSet<string> names = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (Match match in regex.Matches(text))
            {
                string name = Regex.Unescape(match.Groups[1].Value);
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
                {
                    continue;
                }

                result.Add(new OptimisationCatalogueEntry(name));
            }

            return result;
        }

        /// <summary>
        /// The C# text with <c>//</c> and <c>/* */</c> comments replaced by spaces (line breaks kept). String and character
        /// literals, regular and verbatim, are copied as they are, so a "//" inside a string is not a comment.
        /// </summary>
        private static string WithoutComments(string text)
        {
            StringBuilder result = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    while (i < text.Length && text[i] != '\n' && text[i] != '\r')
                    {
                        result.Append(' ');
                        i++;
                    }

                    continue;
                }

                if (c == '/' && next == '*')
                {
                    int end = text.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                    end = end < 0 ? text.Length : end + 2;
                    for (; i < end; i++)
                    {
                        result.Append(text[i] == '\n' || text[i] == '\r' ? text[i] : ' ');
                    }

                    continue;
                }

                if (c == '@' && next == '"')
                {
                    // Verbatim string: "" is an escaped quote; no backslash escapes.
                    result.Append(c).Append(next);
                    i += 2;
                    while (i < text.Length)
                    {
                        result.Append(text[i]);
                        if (text[i] == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                result.Append('"');
                                i += 2;
                                continue;
                            }

                            i++;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    result.Append(c);
                    i++;
                    while (i < text.Length && text[i] != c && text[i] != '\n' && text[i] != '\r')
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            result.Append(text[i]).Append(text[i + 1]);
                            i += 2;
                            continue;
                        }

                        result.Append(text[i]);
                        i++;
                    }

                    if (i < text.Length && text[i] == c)
                    {
                        result.Append(c);
                        i++;
                    }

                    continue;
                }

                result.Append(c);
                i++;
            }

            return result.ToString();
        }
    }
}
