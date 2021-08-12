using System;
using System.IO;
using Alto.CodeAnalysis.Syntax;
using Alto.IO;

namespace Alto.CodeAnalysis.Symbols
{
    internal static class SymbolWriter
    {
        public static void WriteTo(Symbol symbol, TextWriter writer)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.GlobalVariable:
                    WriteGlobalVariableTo((GlobalVariableSymbol)symbol, writer);
                    break;
                case SymbolKind.LocalVariable:
                    WriteLocalVariableTo((LocalVariableSymbol)symbol, writer);
                    break;
                case SymbolKind.Type:
                    WriteTypeTo((TypeSymbol)symbol, writer);
                    break;
                case SymbolKind.Function:
                    WriteFunctionTo((FunctionSymbol)symbol, writer);
                    break;
                case SymbolKind.Parameter:
                    WriteParameterTo((ParameterSymbol)symbol, writer);
                    break;
                default:
                    throw new Exception($"Unexpected symbol: {symbol.Kind}");
            }
        }

        private static void WriteGlobalVariableTo(GlobalVariableSymbol symbol, TextWriter writer)
        {
            writer.WriteKeyword(symbol.IsReadOnly ? SyntaxKind.LetKeyword : SyntaxKind.VarKeyword);
            writer.WriteWhitespace();
            writer.WriteIdentifier(symbol.Name);
            writer.WritePunctuation(SyntaxKind.ColonToken);
            writer.WriteWhitespace();
            symbol.Type.WriteTo(writer);
        }

        private static void WriteLocalVariableTo(LocalVariableSymbol symbol, TextWriter writer)
        {
            writer.WriteKeyword(symbol.IsReadOnly ? SyntaxKind.LetKeyword : SyntaxKind.VarKeyword);
            writer.WriteWhitespace();
            writer.WriteIdentifier(symbol.Name);
            writer.WritePunctuation(SyntaxKind.ColonToken);
            writer.WriteWhitespace();
            symbol.Type.WriteTo(writer);
        }

        private static void WriteTypeTo(TypeSymbol symbol, TextWriter writer)
        {
            writer.WriteIdentifier(symbol.Name);
        }

        private static void WriteFunctionTo(FunctionSymbol symbol, TextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.FunctionKeyword);
            writer.WriteWhitespace();
            writer.WriteIdentifier(symbol.Name);
            writer.WritePunctuation(SyntaxKind.OpenParenthesesToken);

            for (int i = 0; i < symbol.Parameters.Length; i++)
            {
                var p = symbol.Parameters[i];

                if (i > 0)
                {
                    writer.WriteWhitespace();
                    writer.WritePunctuation(SyntaxKind.ColonToken);
                    writer.WriteWhitespace();
                }

                p.WriteTo(writer);
            }
            writer.WritePunctuation(SyntaxKind.CloseParenthesesToken);

            if (symbol.Type != TypeSymbol.Void)
            {
                writer.WriteWhitespace();
                writer.WritePunctuation(SyntaxKind.ColonToken);
                writer.WriteWhitespace();
                symbol.Type.WriteTo(writer);
            }
        }

        private static void WriteParameterTo(ParameterSymbol symbol, TextWriter writer)
        {
            writer.WriteIdentifier(symbol.Name);
            writer.WritePunctuation(SyntaxKind.ColonToken);
            writer.WriteWhitespace();
            symbol.Type.WriteTo(writer); 
        }
    }
}