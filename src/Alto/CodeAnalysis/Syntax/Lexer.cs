using System.Text;
using System.Collections.Generic;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Text;
using System;

namespace Alto.CodeAnalysis.Syntax
{
    internal class Lexer
    {
        private readonly SourceText _text;
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly SyntaxTree _tree;
        private int _position;
        private int _start;
        private SyntaxKind _kind;
        private object _value;
        private bool _isReadingDirective;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Lexer"/> class.
        /// </summary>
        public Lexer(SyntaxTree tree)
        {
            _text = tree.Text;
            _tree = tree;
        }

        /// <summary>
        /// Gets the bag that contains methods for reporting diagnostics and holds all reported diagnostics
        /// throughout the life cycle of the application.
        /// </summary>
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
        
        /// <summary>
        /// Steps the lexing process of converting the input code into a series of <see cref="SyntaxToken"/>.
        /// <summary>
        /// <returns>The <see cref="SyntaxToken"> for the character input</returns>
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
                case '\n':
                case '\r':
                    if (Lookahead == '\n' || Lookahead == '\r')
                        _position++;
                    
                    _position++;
                    _isReadingDirective = false;
                    _kind = SyntaxKind.WhitespaceToken;
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

                case '{':
                    _kind = SyntaxKind.OpenBraceToken;
                    _position++;
                    break;
                case '}':
                    _kind = SyntaxKind.CloseBraceToken;
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
                case '~':
                    if (Lookahead == '=')
                    {
                        _position += 2;
                        _kind = SyntaxKind.BangEqualsToken;
                    }
                    else
                    {
                        _kind = SyntaxKind.TildeToken;
                        _position++;
                    }
                    break;
                case '^':
                    _kind = SyntaxKind.HatToken;
                    _position++;
                    break;
                case '&':
                    _position++;
                    if (Current != '&')
                    {
                        _kind = SyntaxKind.AmpersandToken;
                    }
                    else
                    {
                        _position++;
                        _kind = SyntaxKind.AmpersandAmpersandToken;
                    }
                    break;
                case '|':
                    _position++;
                    if (Current != '|')
                    {
                        _kind = SyntaxKind.PipeToken;
                    }
                    else
                    {
                        _position++;
                        _kind = SyntaxKind.PipePipeToken;
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
                case '<':
                    if (Lookahead != '=')
                    {
                        _position++;
                        _kind = SyntaxKind.LesserToken;
                    }
                    else
                    {
                        _position += 2;
                        _kind = SyntaxKind.LesserOrEqualsToken;
                    }
                    break;
                case '>':
                    if (Lookahead != '=')
                    {
                        _position++;
                        _kind = SyntaxKind.GreaterToken;
                    }
                    else
                    {
                        _position += 2;
                        _kind = SyntaxKind.GreaterOrEqualsToken;
                    }
                    break;
                case ':':
                    _position++;
                    _kind = SyntaxKind.ColonToken;
                    break;
                case '?':
                    _position++;
                    _kind = SyntaxKind.QuestionMarkToken;
                    break;
                case ',':
                    _position++;
                    _kind = SyntaxKind.CommaToken;
                    break;
                case '.':
                    _position++;
                    _kind = SyntaxKind.FullStopToken;
                    break;
                case '"':
                    ReadString();
                    break;
                case '#':
                    ReadDirective();
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
                    else if (char.IsLetter(Current) || Current == '_')
                    {
                        ReadIdentifierOrKeyword();
                    }
                    else
                    {
                        var span = new TextSpan(_position, 1);
                        var location = new TextLocation(_text, span);
                        _diagnostics.ReportBadCharacter(location, Current);
                        _position++;
                    }
                    break;
            }

            var length = _position - _start;
            var text = SyntaxFacts.GetText(_kind);
            if (text == null)
                text = _text.ToString(_start, length);
            
            return new SyntaxToken(_tree, _kind, _start, text, _value);
        }

        private void ReadString()
        {
            // don't care about the first quote
            _position++;

            var sb = new StringBuilder();
            var done = false;
            while (!done)
            {
                switch (Current)
                {
                    case '\0':
                    case '\r':
                    case '\n':
                        var span = new TextSpan(_start, 1);
                        var location = new TextLocation(_text, span);
                        _diagnostics.ReportUnterminatedString(location);
                        done = true;
                        break;
                    case '\\':
                        if (Lookahead == '"')
                        {
                            sb.Append(Lookahead);
                            _position += 2;
                        }
                        else
                        {
                            _position++;
                        }
                        break;
                    case '"':
                        _position++;
                        done = true;
                        break;
                    default:
                        sb.Append(Current);
                        _position++;
                        break;
                }
            }

            _kind = SyntaxKind.StringToken;
            _value = sb.ToString();
        }

        private void ReadDirective()
        {
            _position++;
            _kind = SyntaxKind.HashtagToken;
            _isReadingDirective = true;
        }

        private void ReadWhiteSpaceToken()
        {
            while (char.IsWhiteSpace(Current))
                _position++;

            _kind = SyntaxKind.WhitespaceToken;
        }

        private void ReadIdentifierOrKeyword()
        {
            while (char.IsLetter(Current) || Current == '_')
                _position++;
            
            var length = _position - _start;
            var text = _text.ToString(_start, length);

            if (!_isReadingDirective)
                _kind = SyntaxFacts.GetKeywordKind(text);
            else
                _kind = SyntaxKind.IdentifierToken;
        }

        private void ReadNumberToken()
        {
            while (char.IsDigit(Current))
                _position++;
            
            var length = _position - _start;
            var text = _text.ToString(_start, length);
            if (!int.TryParse(text, out var value))
            {
                var span = new TextSpan(_start, length);
                var location = new TextLocation(_text, span);
                _diagnostics.ReportInvalidNumber(location, text, TypeSymbol.Int);
            }

            _kind = SyntaxKind.NumberToken;
            _value = value;
        }
    }
}