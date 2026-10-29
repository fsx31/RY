using System;
using System.IO;
using System.Collections.Generic;

namespace RY.Compiler
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("RY v0.0 - Standalone Interpreter");
                Console.WriteLine("Usage: ry <path-to-script.ry>");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            string filePath = args[0];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Error]: Could not find file '{filePath}'");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            string sourceCode = File.ReadAllText(filePath);

            Console.WriteLine($"Running RY source from: {filePath}\n");

            try
            {
                Lexer lexer = new Lexer(sourceCode);
                List<Token> tokens = lexer.Tokenize();

                Console.WriteLine("Output:");
                Interpreter interpreter = new Interpreter(tokens);
                interpreter.Interpret();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[RY Runtime Error]: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}