using System;

namespace RY.Compiler
{
    public enum TokenType
    {
        Keyword,
        Identifier,
        Number,
        String,
        Symbol,
        Operator,
        Macro,
        Comment
    }

    public record Token(TokenType Type, string Value);
}