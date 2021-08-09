using System.Text;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis.Syntax
{

    /// <summary>
    /// Here, we lex the input text into their respective SyntaxTokens.
    /// </summary>

    internal class Lexer
    {
        private readonly SourceText _text;
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly SyntaxTree _tree;
        private int _position;
        private int _start;
        private SyntaxKind _kind;
        private object _value;
        
        public Lexer(SyntaxTree tree)
        {
            _text = tree.Text;
            _tree = tree;
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
                case '"':
                    ReadString();
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
            _kind = SyntaxFacts.GetKeywordKind(text);
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