using System;
using System.Collections.Generic;
using System.Linq;
using Alto.CodeAnalysis.Syntax;
using System.Collections.Immutable;
using Alto.CodeAnalysis;
using Alto.CodeAnalysis.Text;
using Alto.CodeAnalysis.Syntax.Preprocessing;

namespace Alto.CodeAnalysis.Syntax
{
    /// <summary>
    /// Represents the class to do semantic analysis in. This class cannot be inherited. 
    /// </summary>
    internal sealed class Parser
    {
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly ImmutableArray<SyntaxToken> _tokens;
        private readonly SourceText _text;
        private readonly SyntaxTree _tree;
        private int _position;
        private bool _parsingMemberAccessExpression = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="Parser"/> class.
        /// </summary>
        /// <param name="tree">The <see cref="SyntaxTree"> to initialize the parser for.</param>
        public Parser(SyntaxTree tree)
        {
            var tokens = new List<SyntaxToken>();
            Lexer lexer = new Lexer(tree);
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                if (token.Kind != SyntaxKind.WhitespaceToken && token.Kind != SyntaxKind.BadToken)
                    tokens.Add(token);
            } while (token.Kind != SyntaxKind.EndOfFileToken);

            _tokens = tokens.ToImmutableArray();
            _diagnostics.AddRange(lexer.Diagnostics);
            _text = tree.Text;
            _tree = tree;
        }

        /// <summary>
        /// Gets the collection of diagnostics to report.
        /// </summary>
        /// <remarks>Also contains methods for error reporting.</remarks>
        public DiagnosticBag Diagnostics => _diagnostics;

        private SyntaxToken Peek(int offset)
        {
            var index = _position + offset;
            if (index >= _tokens.Length)
                return _tokens[_tokens.Length - 1];
            
            return _tokens[index];
        }

        private SyntaxToken Current => Peek(0);
        
        private SyntaxToken NextToken()
        {
           var current = Current;
           _position++;
           return current;
        }
        
        private SyntaxToken MatchToken(SyntaxKind kind)
        {
            if (Current.Kind == kind)
                return NextToken(); 

            _diagnostics.ReportUnexpectedToken(Current.Location, Current.Kind, kind);
            return new SyntaxToken(_tree, kind, Current.Position, null, null);
        }

        internal CompilationUnitSyntax ParseCompilationUnit()
        {
            var members = ParseMembers();
            var endOfFileToken = MatchToken(SyntaxKind.EndOfFileToken);

            Preprocessor preprocessor = new Preprocessor(members);
            var processedMembers = preprocessor.Process();
            _diagnostics.AddRange(preprocessor.Diagnostics);

            return new CompilationUnitSyntax(_tree, processedMembers, endOfFileToken);
        }

        private ImmutableArray<MemberSyntax> ParseMembers()
        {
            var members = ImmutableArray.CreateBuilder<MemberSyntax>();

            while (Current.Kind != SyntaxKind.EndOfFileToken)
            {
                var startToken = Current;

                var statement = ParseMember();
                members.Add(statement);

                // Skip current token in order to avoide an infinite loop.
                if (Current == startToken)
                    NextToken();
            }

            return members.ToImmutable();
        }

        private MemberSyntax ParseMember()
        {
            if (Current.Kind == SyntaxKind.FunctionKeyword)
                return ParseFunctionDeclaration();
            
            return ParseGlobalStatement();
        }

        private MemberSyntax ParseGlobalStatement()
        {
            if (Current.Kind == SyntaxKind.HashtagToken)
                return ParseDirective();
            
            var statement = ParseStatement();
            return new GlobalStatementSyntax(_tree, statement);
        }

        private FunctionDeclarationSyntax ParseFunctionDeclaration()
        {
            var keyword = MatchToken(SyntaxKind.FunctionKeyword);
            var identifier = MatchToken(SyntaxKind.IdentifierToken);

            var openParenthesis = MatchToken(SyntaxKind.OpenParenthesesToken);
            var parameters = ParseParameterList();
            var closedParenthesis = MatchToken(SyntaxKind.CloseParenthesesToken);

            var type = ParseOptionalTypeClause();
            var body = ParseBlockStatement();

            return new FunctionDeclarationSyntax(_tree, keyword, identifier, openParenthesis, parameters, closedParenthesis, type, body);
        }



        private SeparatedSyntaxList<ParameterSyntax> ParseParameterList()
        {
            var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();

            var parseNextParameter = true;
            ParameterSyntax lastParamerer = null;
            while (parseNextParameter &&
                   Current.Kind != SyntaxKind.CloseParenthesesToken &&
                   Current.Kind != SyntaxKind.EndOfFileToken)
            {
                var parameter = ParseParameter();
                nodesAndSeparators.Add(parameter);

                if (Current.Kind == SyntaxKind.CommaToken)
                {   
                    var comma =  MatchToken(SyntaxKind.CommaToken);
                    nodesAndSeparators.Add(comma);
                }
                else
                {
                    parseNextParameter = false;
                }

                if (lastParamerer != null)
                    if (lastParamerer.IsOptional && !parameter.IsOptional)
                        _diagnostics.ReportOptionalParametersMustAppearLast(parameter.Location);

                lastParamerer = parameter;
            }
            return new SeparatedSyntaxList<ParameterSyntax>(nodesAndSeparators.ToImmutable());
        }

        private ParameterSyntax ParseParameter()
        {
            var identifier = MatchToken(SyntaxKind.IdentifierToken);
            var type = ParseTypeClause();

            // also have to parse optional default value
            // have to make sure it's optional
            SyntaxToken equalsToken = null;
            bool isOptional = false;
            var k = Peek(0).Kind;
            if (k == SyntaxKind.EqualsToken)
            {
                equalsToken = MatchToken(SyntaxKind.EqualsToken);
                isOptional = equalsToken.Text != null;
            }

            ExpressionSyntax optionalExpression = null;
            if (isOptional)
                optionalExpression = ParseExpression();

            return new ParameterSyntax(_tree, identifier, type, isOptional, optionalExpression);
        }

        private StatementSyntax ParseStatement()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenBraceToken:
                    return ParseBlockStatement();
                case SyntaxKind.LetKeyword:
                case SyntaxKind.VarKeyword:
                    return ParseVariableDeclaration();
                case SyntaxKind.IfKeyword:
                    return ParseIfStatement();
                case SyntaxKind.WhileKeyword:
                    return ParseWhileStatement();
                case SyntaxKind.DoKeyword:
                    return ParseDoWhileStatement();
                case SyntaxKind.ForKeyword:
                    return ParseForStatement();
                case SyntaxKind.BreakKeyword:
                    return ParseBreakStatement();
                case SyntaxKind.ContinueKeyword:
                    return ParseContinueStatement();
                case SyntaxKind.ReturnKeyword:
                    return ParseReturnStatement();
                default:
                    return ParseExpressionStatement();
            }
        }

        private StatementSyntax ParseVariableDeclaration()
        {
            var expected = Current.Kind == SyntaxKind.LetKeyword ? SyntaxKind.LetKeyword : SyntaxKind.VarKeyword;
            var keyword = MatchToken(expected);
            var identifier = MatchToken(SyntaxKind.IdentifierToken);
            var typeClause = ParseOptionalTypeClause();
            var equals = MatchToken(SyntaxKind.EqualsToken);
            var initializer = ParseExpression();

            return new VariableDeclarationSyntax(_tree, keyword, identifier, typeClause, equals, initializer);
        }

        private TypeClauseSyntax ParseOptionalTypeClause()
        {
            if (Current.Kind != SyntaxKind.ColonToken)
                return null;

            return ParseTypeClause();
        }

        private TypeClauseSyntax ParseTypeClause()
        {
            var colonToken = MatchToken(SyntaxKind.ColonToken);
            var identifier = MatchToken(SyntaxKind.IdentifierToken);

            return new TypeClauseSyntax(_tree, colonToken, identifier);
        }

        private StatementSyntax ParseIfStatement()
        {
            var keyword = MatchToken(SyntaxKind.IfKeyword);
            var condition = ParseExpression();
            var statement = ParseStatement();
            var elseClause = ParseElseClause();
            return new IfStatementSyntax(_tree, keyword, condition, statement, elseClause);
        }

        private StatementSyntax ParseForStatement()
        {
            var keyword = MatchToken(SyntaxKind.ForKeyword);
            var identifier = MatchToken(SyntaxKind.IdentifierToken);
            var equalsToken = MatchToken(SyntaxKind.EqualsToken);
            var lowerBound = ParseExpression();
            var toKeyword = MatchToken(SyntaxKind.ToKeyword);
            var upperBound = ParseExpression();
            var body = ParseStatement();
            return new ForStatementSyntax(_tree, keyword, identifier, equalsToken, lowerBound, toKeyword, upperBound, body);
        }

        private ElseClauseSyntax ParseElseClause()
        {
            if (Current.Kind != SyntaxKind.ElseKeyword)
                return null;

            var keyword = NextToken();
            var statement = ParseStatement();
            return new ElseClauseSyntax(_tree, keyword, statement);
        }

        private StatementSyntax ParseWhileStatement()
        {
            var keyword = MatchToken(SyntaxKind.WhileKeyword);
            var condition = ParseExpression();
            var body = ParseStatement();

            return new WhileStatementSyntax(_tree, keyword, condition, body);
        }

        private StatementSyntax ParseDoWhileStatement()
        {
            var doKeyword = MatchToken(SyntaxKind.DoKeyword);
            var body = ParseStatement();
            var whileKeyword = MatchToken(SyntaxKind.WhileKeyword);
            var condition = ParseExpression();

            return new DoWhileStatementSyntax(_tree, doKeyword, body, whileKeyword, condition);
        }

        private StatementSyntax ParseBreakStatement()
        {
            var keyword = MatchToken(SyntaxKind.BreakKeyword);
            return new BreakStatementSyntax(_tree, keyword);
        }

        private StatementSyntax ParseContinueStatement()
        {
            var keyword = MatchToken(SyntaxKind.ContinueKeyword);
            return new ContinueStatementSyntax(_tree, keyword);
        }

        private StatementSyntax ParseReturnStatement()
        {
            var keyword = MatchToken(SyntaxKind.ReturnKeyword);

            var keywordLine = _text.GetLineIndex(keyword.Span.Start);
            var currentLine = _text.GetLineIndex(Current.Span.Start);
            var isEof = Current.Kind == SyntaxKind.EndOfFileToken; 
            var sameLine = !isEof && currentLine == keywordLine;
            
            var expression =  sameLine ? ParseExpression() : null;

            return new ReturnStatementSyntax(_tree, keyword, expression);
        }

        private BlockStatementSyntax ParseBlockStatement()
        {
            var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
            var functions = ImmutableArray.CreateBuilder<FunctionDeclarationSyntax>();
            var openBraceToken = MatchToken(SyntaxKind.OpenBraceToken);

            while (Current.Kind != SyntaxKind.EndOfFileToken && Current.Kind != SyntaxKind.CloseBraceToken)
            {
                var startToken = Current;

                if (startToken.Kind == SyntaxKind.FunctionKeyword)
                {
                    var declaration = ParseFunctionDeclaration();
                    functions.Add(declaration);
                }
                else    
                {
                    var statement = ParseStatement();
                    statements.Add(statement);
                }

                // Skip current token in order to avoide an infinite loop.
                if (Current == startToken)
                    NextToken();
            }

            var closeBraceToken = MatchToken(SyntaxKind.CloseBraceToken);
            return new BlockStatementSyntax(_tree, openBraceToken, statements.ToImmutable(), functions.ToImmutable(), closeBraceToken);
        }

        private ExpressionStatementSyntax ParseExpressionStatement()
        {
            var expression = ParseExpression();
            return new ExpressionStatementSyntax(_tree, expression);
        }

        private  ExpressionSyntax ParseExpression()
        {
            return ParseAssignmentExpression();
        }

        private ExpressionSyntax ParseAssignmentExpression()
        {
            if (Peek(0).Kind == SyntaxKind.IdentifierToken)
            {
                switch (Peek(1).Kind)
                {
                    case SyntaxKind.EqualsToken:
                        var identifierToken = NextToken();
                        var operatorToken = NextToken();
                        var right = ParseAssignmentExpression();
                        return new AssignmentExpressionSyntax(_tree, identifierToken, operatorToken, right);
                }
            }

            return ParseBinaryExpression();
        }

        private ExpressionSyntax ParseBinaryExpression(int parentPrecedence = 0)
        {
            ExpressionSyntax left;
            var unaryOperatorPrecedence = Current.Kind.GetUnaryOperatorPrecedence();
            if (unaryOperatorPrecedence != 0 && unaryOperatorPrecedence >= parentPrecedence)
            {
                var operatorToken = NextToken();
                var operand = ParseBinaryExpression();
                left = new UnaryExpressionSyntax(_tree, operatorToken, operand);
            } 
            else
            {
                left = ParsePrimaryExpression();
            }

            while (true)
            {
                var precedence = Current.Kind.GetBinaryOperatorPrecedence();
                if (precedence == 0 || precedence <= parentPrecedence)
                    break;
                
                var operatorToken = NextToken();
                var right = ParseBinaryExpression(precedence);
                left = new BinaryExpressionSyntax(_tree, left, operatorToken, right); 
            }
            return left;    
        }
        
        private ExpressionSyntax ParsePrimaryExpression()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenParenthesesToken:
                    return ParseParenthesizedExpression();
                case SyntaxKind.FalseKeyword:
                case SyntaxKind.TrueKeyword:
                    return ParseBooleanLiteral();
                case SyntaxKind.NumberToken:
                    return ParseNumberLiteral();
                case SyntaxKind.StringToken:
                    return ParseStringLiteral();
                case SyntaxKind.NewKeyword:
                    return ParseObjectCreationExpression();
                case SyntaxKind.IdentifierToken:
                default:
                    return ParseNameOrCallOrMemberAccessExpression();
            }
        }

        private ExpressionSyntax ParseNumberLiteral()
        {
            var stringToken = MatchToken(SyntaxKind.NumberToken);
            return new LiteralExpressionSyntax(_tree, stringToken);
        }

        private ExpressionSyntax ParseStringLiteral()
        {
            var numberToken = MatchToken(SyntaxKind.StringToken);
            return new LiteralExpressionSyntax(_tree, numberToken);
        }

        private ExpressionSyntax ParseParenthesizedExpression()
        {
            var left = MatchToken(SyntaxKind.OpenParenthesesToken);
            var expression = ParseExpression();
            var right = MatchToken(SyntaxKind.CloseParenthesesToken);
            return new ParenthesizedExpressionSyntax(_tree, left, expression, right);
        }
 
        private ExpressionSyntax ParseBooleanLiteral()
        {
            var isTrue = Current.Kind == SyntaxKind.TrueKeyword;
            var keywordToken = isTrue ? MatchToken(SyntaxKind.TrueKeyword) : MatchToken(SyntaxKind.FalseKeyword);
            var value = keywordToken.Kind == SyntaxKind.TrueKeyword;
            return new LiteralExpressionSyntax(_tree, Current, value);
        }

        private ExpressionSyntax ParseObjectCreationExpression()
        {
            var newKeyword = MatchToken(SyntaxKind.NewKeyword);
            var type = MatchToken(SyntaxKind.IdentifierToken);
            var openParenthesisToken = MatchToken(SyntaxKind.OpenParenthesesToken);
            var args = ParseArguments();
            var closedParenthesisToken = MatchToken(SyntaxKind.CloseParenthesesToken);

            return new ObjectCreationExpressionSyntax(_tree, newKeyword, type, openParenthesisToken, args, closedParenthesisToken);
        }


        private ExpressionSyntax ParseNameOrCallOrMemberAccessExpression()
        {
            if (Peek(0).Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.OpenParenthesesToken)
                return ParseCallExpression();

            if (!_parsingMemberAccessExpression && Peek(0).Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.FullStopToken)
                return ParseMemberAcessExpression();

            return ParseNameExpression();
        }

        private ExpressionSyntax ParseCallExpression()
        {
            var identifier = MatchToken(SyntaxKind.IdentifierToken);
            var openParenthesisToken = MatchToken(SyntaxKind.OpenParenthesesToken);
            var args = ParseArguments();
            var closedParenthesisToken = MatchToken(SyntaxKind.CloseParenthesesToken);
            return new CallExpressionSyntax(_tree, identifier, openParenthesisToken, args, closedParenthesisToken);
        }

        private ExpressionSyntax ParseMemberAcessExpression()
        {
            // TODO: Allow for member access chaining: obj.obj.obj.obj = false
            _parsingMemberAccessExpression = true;
            var left = ParseExpression();
            var fullStop = MatchToken(SyntaxKind.FullStopToken);
            var right = ParseNameOrCallOrMemberAccessExpression();
            _parsingMemberAccessExpression = false;

            return new MemberAccessExpression(_tree, left, fullStop, right);
        }

        private SeparatedSyntaxList<ExpressionSyntax> ParseArguments()
        {   
            var nodesAndSeparators = ImmutableArray.CreateBuilder<SyntaxNode>();

            var parseNextArgument = true;
            while (parseNextArgument && 
                   Current.Kind != SyntaxKind.CloseParenthesesToken &&
                   Current.Kind != SyntaxKind.EndOfFileToken)
            {
                var expression = ParseExpression();
                nodesAndSeparators.Add(expression);

                if (Current.Kind == SyntaxKind.CommaToken)
                {
                    var comma =  MatchToken(SyntaxKind.CommaToken);
                    nodesAndSeparators.Add(comma);
                }
                else
                {
                    parseNextArgument = false;
                }
            }
            return new SeparatedSyntaxList<ExpressionSyntax>(nodesAndSeparators.ToImmutable());
        }

        private ExpressionSyntax ParseNameExpression()
        {
            var identifierToken = MatchToken(SyntaxKind.IdentifierToken);
            return new NameExpressionSyntax(_tree, identifierToken);
        }

        private PreprocessorDirective ParseDirective()
        {
            var hashtag = MatchToken(SyntaxKind.HashtagToken);
            var startLine = hashtag.Location.StartLine;

            List<SyntaxToken> identifiers = new List<SyntaxToken>();
            while (true)
            {
                if (Current.Location.StartLine != startLine ||
                    Current.Kind == SyntaxKind.EndOfFileToken)
                {
                    break;
                }
                
                var identifier = MatchToken(SyntaxKind.IdentifierToken);
                if (identifier.Text == null)
                {
                    NextToken();
                    continue;
                }
                
                identifiers.Add(identifier);
            }

            var kind = Preprocessor.ClassifyDirective(identifiers.FirstOrDefault().Text);
            if (kind == null)
                _diagnostics.ReportDirectiveExpected(identifiers.FirstOrDefault().Location);

            return new PreprocessorDirective(hashtag.SyntaxTree, kind, identifiers);
        } 
    }
}