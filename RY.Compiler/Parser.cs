using System;
using System.Text;
using System.Collections.Generic;

namespace RY.Compiler
{
    public record LogicBranch(string Condition, string Body);

    public class Parser
    {
        private readonly string[] _lines;
        private int _index;

        public Parser(string[] lines)
        {
            _lines = lines;
            _index = 0;
        }

        public List<LogicBranch> ParseIfChain()
        {
            List<LogicBranch> branches = new();
            int headerIndex = _index;

            while (true)
            {
                string header = _lines[headerIndex].Trim();
                string keyword = header.StartsWith("if", StringComparison.Ordinal)
                    ? "if"
                    : "elseif";

                string condition = ExtractCondition(header, keyword);
                string body = ExtractBlock(_lines, headerIndex, out int endIndex);

                branches.Add(new LogicBranch(condition, body));

                int nextIndex = endIndex + 1;

                while (nextIndex < _lines.Length &&
                       string.IsNullOrWhiteSpace(_lines[nextIndex]))
                {
                    nextIndex++;
                }

                if (nextIndex < _lines.Length &&
                    _lines[nextIndex].Trim().StartsWith("elseif", StringComparison.Ordinal))
                {
                    headerIndex = nextIndex;
                }
                else
                {
                    while (nextIndex < _lines.Length && string.IsNullOrWhiteSpace(_lines[nextIndex]))
                    {
                        nextIndex++;
                    }

                    if (nextIndex < _lines.Length && _lines[nextIndex].Trim().StartsWith("else", StringComparison.Ordinal))
                    {
                        string elseBody = ExtractBlock(_lines, nextIndex, out int elseEndIndex);
                        branches.Add(new LogicBranch("true", elseBody));
                        _index = elseEndIndex;
                    }
                    else
                    {
                        _index = endIndex;
                    }

                    return branches;
                }
            }
        }

        private static string ExtractCondition(string header, string keyword)
        {
            int open = header.IndexOf('(');
            int close = header.LastIndexOf(')');

            if (!header.StartsWith(keyword, StringComparison.Ordinal) ||
                open < 0 || close <= open)
            {
                throw new FormatException($"Invalid {keyword} statement: {header}");
            }

            return header.Substring(open + 1, close - open - 1).Trim();
        }

        private static string ExtractBlock(
            string[] lines,
            int headerIndex,
            out int endIndex)
        {
            int braceLine = headerIndex;
            int openBrace = lines[braceLine].IndexOf('{');

            if (openBrace < 0)
            {
                braceLine++;

                while (braceLine < lines.Length &&
                       string.IsNullOrWhiteSpace(lines[braceLine]))
                {
                    braceLine++;
                }

                if (braceLine >= lines.Length ||
                    lines[braceLine].Trim() != "{")
                {
                    throw new FormatException("Expected '{' after the condition.");
                }

                openBrace = lines[braceLine].IndexOf('{');
            }

            int depth = 1;
            StringBuilder body = new();

            for (int i = braceLine; i < lines.Length; i++)
            {
                int start = i == braceLine ? openBrace + 1 : 0;
                string line = lines[i];

                for (int j = start; j < line.Length; j++)
                {
                    if (line[j] == '{')
                    {
                        depth++;
                    }
                    else if (line[j] == '}')
                    {
                        depth--;

                        if (depth == 0)
                        {
                            if (j > start)
                            {
                                body.Append(line.Substring(start, j - start));
                            }

                            endIndex = i;
                            return body.ToString().Trim();
                        }
                    }
                }

                if (line.Length > start)
                {
                    body.AppendLine(line.Substring(start));
                }
                else
                {
                    body.AppendLine();
                }
            }

            throw new FormatException("Missing closing '}'.");
        }
    }
}