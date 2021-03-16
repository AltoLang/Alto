using System;
using System.Collections.Generic;
using System.Linq;

namespace compiler.CodeAnalysis.Syntax
{
    internal class Lexer
    {
        private readonly string _text;
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private int _position;
        private int _start;
        private SyntaxKind _kind;
        private object _value;
        public Lexer(string text)
        {
            _text = text;
        }

        public DiagnosticBag Diagnostics => _diagnostics;
        private char Current => Peek(0);
        private char Lookahead => Peek(1);

        private char Peek(int offset)
        {
            var index = _position + offset;
            if (index >= _text.Length)
                return '\0';
            return _text[index];
        }

        public SyntaxToken Lex()
        {
            _start = _position;
            _kind = SyntaxKind.BadToken;
            _value = null;

            switch (Current)
            {
                case '\0':
                    _kind = SyntaxKind.EndOfFileToken;
                    break;
                case '+':
                    _kind = SyntaxKind.PlusToken;
                    _position++;
                    break;
                case '-':
                    _kind = SyntaxKind.MinusToken;
                    _position++;
                    break;
                case '*':
                    _kind = SyntaxKind.StarToken;
                    _position++;
                    break;
                case '/':
                    _kind = SyntaxKind.SlashToken;
                    _position++;
                    break;
                case '(':
                    _kind = SyntaxKind.OpenParenthesesToken;
                    _position++;
                    break;
                case ')':
                    _kind = SyntaxKind.CloseParenthesesToken;
                    _position++;
                    break;
                case '%':
                    _kind = SyntaxKind.PercentageToken;
                    _position++;
                    break;
                case '!':
                    _kind = SyntaxKind.BangToken;
                    _position++;
                    break;
                case '&':
                    if (Lookahead == '&')
                    {
                        _kind = SyntaxKind.AmpersandAmpersandToken;
                        _position += 2;
                    }
                    break;
                case '|':
                    if (Lookahead == '|')
                    {
                        _kind = SyntaxKind.PipePipeToken;
                        _position += 2;
                    }
                    break;
                case '=':
                    _position++;
                    if (Current != '=')
                    {
                        _kind = SyntaxKind.EqualsToken;
                    }
                    else
                    {
                        _position++;
                        _kind = SyntaxKind.EqualsEqualsToken;
                    }
                    break;
                case '~':
                    if (Lookahead == '=')
                    {
                        _position += 2;
                        _kind = SyntaxKind.BangEqualsToken;
                    }
                    break;


                default:

                    if (char.IsDigit(Current))
                    {
                        ReadNumberToken();
                    }
                    else if (char.IsWhiteSpace(Current))
                    {
                        ReadWhiteSpaceToken();
                    }
                    else if (char.IsLetter(Current))
                    {
                        ReadIdentifierOrKeyword();
                    }
                    else
                    {
                        _diagnostics.ReportBadCharacter(_position, Current);
                        _position++;
                    }
                    break;
            }

            var length = _position - _start;
            var text = SyntaxFacts.GetText(_kind);
            if (text == null)
                text = _text.Substring(_start, length);

            return new SyntaxToken(_kind, _start, text, _value);
        }

        private void ReadWhiteSpaceToken()
        {
            while (char.IsWhiteSpace(Current))
                _position++;

            _kind = SyntaxKind.WhitespaceToken;
        }

        private void ReadIdentifierOrKeyword()
        {
            while (char.IsLetter(Current))
                _position++;
            
            var length = _position - _start;
            _kind = SyntaxFacts.GetKeywordKind(_text.Substring(_start, length));
        }

        private void ReadNumberToken()
        {
            while (char.IsDigit(Current))
                _position++;
            
            var length = _position - _start;
            var text = _text.Substring(_start, length);
            if (!int.TryParse(text, out var value))
                _diagnostics.ReportInvalidNumber(new TextSpan(_start, length), _text, typeof(int));

            _kind = SyntaxKind.NumberToken;
            _value = value;
        }
    }
}