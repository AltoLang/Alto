using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis.Syntax
{
    internal class Lexer
    {
        private readonly SourceText _text;
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private int _position;
        private int _start;
        private SyntaxKind _kind;
        private object _value;
        public Lexer(SourceText text)
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
                        _diagnostics.ReportBadCharacter(_position, Current);
                        _position++;
                    }
                    break;
            }

            var length = _position - _start;
            var text = SyntaxFacts.GetText(_kind);
            if (text == null)
                text = _text.ToString(_start, length);
            
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
                _diagnostics.ReportInvalidNumber(new TextSpan(_start, length), text, typeof(int));

            _kind = SyntaxKind.NumberToken;
            _value = value;
        }
    }
}