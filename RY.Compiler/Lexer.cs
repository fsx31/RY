using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace RY.Compiler
{
    public class Lexer
    {
        private readonly string _source;
        private int _position;

        private static readonly HashSet<string> Keywords = new()
        {
            "if", "elseif", "else", "return", "and", "or", "function", "do", "true", "false"
        };

        public Lexer(string source)
        {
            _source = source;
            _position = 0;
        }

        public List<Token> Tokenize()
        {
            List<Token> tokens = new();

            while (_position < _source.Length)
            {
                char current = _source[_position];

                if (char.IsWhiteSpace(current)) // Skip whitespace
                {
                    _position++;
                    continue;
                }

                if (char.IsLetter(current)) // Identifiers & Keywords
                {
                    int start = _position;
                    while (_position < _source.Length && char.IsLetterOrDigit(_source[_position]))
                    {
                        _position++;
                    }
                    string value = _source.Substring(start, _position - start);

                    TokenType type = Keywords.Contains(value) 
                        ? TokenType.Keyword 
                        : TokenType.Identifier;

                    tokens.Add(new Token(type, value));
                    continue;
                }

                if (char.IsDigit(current)) // Handle numbers
                {
                    int start = _position;
                    bool hasDecimal = false;

                    while (_position < _source.Length)
                    {
                        char c = _source[_position];
                        if (char.IsDigit(c))
                        {
                            _position++;
                        }
                        else if (c == '.' && !hasDecimal)
                        {
                            hasDecimal = true;
                            _position++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    string value = _source.Substring(start, _position - start);
                    tokens.Add(new Token(TokenType.Number, value));
                    continue;
                }

                if (_position + 1 < _source.Length)
                {
                    string twoCharOp = $"{current}{_source[_position + 1]}";
                    if (twoCharOp is "==" or "+=" or "-=" or "*=" or ">=" or "<=")
                    {
                        tokens.Add(new Token(TokenType.Operator, twoCharOp));
                        _position += 2;
                        continue;
                    }
                }

                if ("=<>+-*/".Contains(current))
                {
                    tokens.Add(new Token(TokenType.Operator, current.ToString()));
                    _position++;
                    continue;
                }

                if (current == '"')
                {
                    _position++;
                    int start = _position;
                    while (_position < _source.Length && _source[_position] != '"')
                    {
                        _position++;
                    }

                    if (_position >= _source.Length)
                    {
                        throw new Exception("Unterminated string literal.");
                    }

                    string value = _source.Substring(start, _position - start);
                    _position++;

                    tokens.Add(new Token(TokenType.String, value));
                    continue;
                }

                // Symbols
                if ("{}();.".Contains(current))
                {
                    tokens.Add(new Token(TokenType.Symbol, current.ToString()));
                    _position++;
                    continue;
                }

                // Macros
                if (current == '#')
                {
                    tokens.Add(new Token(TokenType.Macro, current.ToString()));
                    _position++;
                    continue;
                }

                // Comments
                if (current == '?')
                {
                    int start = _position;

                    while (_position < _source.Length && _source[_position] != '\n' && _source[_position] != '\r')
                    {
                        _position++;
                    }
    
                    string commentText = _source.Substring(start, _position - start);
                    tokens.Add(new Token(TokenType.Comment, commentText));
                    continue;
                }

                _position++; 
            }

            return tokens; 
        }
    }
}