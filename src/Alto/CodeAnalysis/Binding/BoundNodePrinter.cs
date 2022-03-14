using System;
using System.CodeDom.Compiler;
using System.IO;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;
using Alto.IO;

namespace Alto.CodeAnalysis.Binding
{
    internal static class BoundNodePrinter
    {
        public static void WriteTo(this BoundNode node, TextWriter writer)
        {
            if (writer is IndentedTextWriter iw)
                WriteTo(node, iw);
            else
                WriteTo(node, new IndentedTextWriter(writer));
        }
        private static void WriteNestedStatement(this IndentedTextWriter writer, BoundStatement node)
        {
            var needsIndent = !(node is BoundBlockStatement);
            if (needsIndent)
                writer.Indent++;
            
            node.WriteTo(writer);

            if (needsIndent)
                writer.Indent--;
        }

        private static void WriteNestedExpression(this IndentedTextWriter writer, int parentPrecedence, BoundExpression expression)
        {
            if (expression is BoundUnaryExpression u)
                writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetUnaryOperatorPrecedence(u.Op.SyntaxKind), u);
            if (expression is BoundBinaryExpression b)
                writer.WriteNestedExpression(parentPrecedence, SyntaxFacts.GetBinaryOperatorPrecedence(b.Op.SyntaxKind), b);
            else
                expression.WriteTo(writer);
        }

        private static void WriteNestedExpression(this IndentedTextWriter writer, int parentPrecedence, int currentPrecedence, BoundExpression expression)
        {
            var needsParenthesis = parentPrecedence >= currentPrecedence;
            if (needsParenthesis)
                writer.WritePunctuation(SyntaxKind.OpenParenthesesToken);

            expression.WriteTo(writer);

            if (needsParenthesis)
                writer.WritePunctuation(SyntaxKind.CloseParenthesesToken);
        }

        public static void WriteTo(this BoundNode node, IndentedTextWriter writer)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.ErrorExpression:
                    WriteErrorExpression((BoundErrorExpression)node, writer);
                    break;
                case BoundNodeKind.UnaryExpression:
                    WriteUnaryExpression((BoundUnaryExpression)node, writer);
                    break;
                case BoundNodeKind.LiteralExpression:
                    WriteLiteralExpression((BoundLiteralExpression)node, writer);
                    break;
                case BoundNodeKind.VariableExpression:
                    WriteVariableExpression((BoundVariableExpression)node, writer);
                    break;
                case BoundNodeKind.TypeExpression:
                    WriteTypeExpression((BoundTypeExpression)node, writer);
                    break;
                case BoundNodeKind.AssignmentExpression:
                    WriteAssignmentExpression((BoundAssignmentExpression)node, writer);
                    break;
                case BoundNodeKind.BinaryExpression:
                    WriteBinaryExpression((BoundBinaryExpression)node, writer);
                    break;
                case BoundNodeKind.CallExpression:
                    WriteCallExpression((BoundCallExpression)node, writer);
                    break;
                case BoundNodeKind.ConversionExpression:
                    WriteConversionExpression((BoundConversionExpression)node, writer);
                    break;
                case BoundNodeKind.MemberAccessExpression:
                    WriteMemberAccessExpression((BoundMemberAccessExpression)node, writer);
                    break;
                case BoundNodeKind.BlockStatement:
                    WriteBlockStatement((BoundBlockStatement)node, writer);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    WriteExpressionStatement((BoundExpressionStatement)node, writer);
                    break;
                case BoundNodeKind.VariableDeclaration:
                    WriteVariableDeclaration((BoundVariableDeclaration)node, writer);
                    break;
                case BoundNodeKind.IfStatement:
                    WriteIfStatement((BoundIfStatement)node, writer);
                    break;
                case BoundNodeKind.WhileStatement:
                    WriteWhileStatement((BoundWhileStatement)node, writer);
                    break;
                case BoundNodeKind.DoWhileStatement:
                    WriteDoWhileStatement((BoundDoWhileStatement)node, writer);
                    break;
                case BoundNodeKind.ForStatement:
                    WriteForStatement((BoundForStatement)node, writer);
                    break;
                case BoundNodeKind.GotoStatement:
                    WriteGotoStatement((BoundGotoStatement)node, writer);
                    break;
                case BoundNodeKind.ConditionalGotoStatement:
                    WriteConditionalGotoStatement((BoundConditionalGotoStatement)node, writer);
                    break;
                case BoundNodeKind.LabelStatement:
                    WriteLabelStatement((BoundLabelStatement)node, writer);
                    break;
                case BoundNodeKind.ReturnStatement:
                    WriteReturnStatement((BoundReturnStatement)node, writer);
                    break;
                default:
                    throw new Exception($"Unexprected BoundNodeKind {node.Kind}");
            }
        }

        private static void WriteErrorExpression(BoundErrorExpression node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.QuestionMarkToken);
        }

        private static void WriteUnaryExpression(BoundUnaryExpression node, IndentedTextWriter writer)
        {
            var precedence = SyntaxFacts.GetUnaryOperatorPrecedence(node.Op.SyntaxKind);

            writer.WritePunctuation(node.Op.SyntaxKind);

            writer.WriteNestedExpression(precedence, node.Operand);
            node.Operand.WriteTo(writer);
        }

        private static void WriteLiteralExpression(BoundLiteralExpression node, IndentedTextWriter writer)
        {
            var v = node.Value.ToString();

            if (node.Type == TypeSymbol.Bool)
            {
                writer.WriteBool(v);
            }
            else if (node.Type == TypeSymbol.Int)
            {
                writer.WriteNumber(v);
            }
            else if (node.Type == TypeSymbol.String)
            {
                v = "\"" + v.Replace("\"", "\u0022")  + "\"";
                writer.WriteString(v);
            }
            else
            {
                throw new Exception($"Unexpected type {node.Type}.");
            }
        }

        private static void WriteVariableExpression(BoundVariableExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Variable.Name);
        }

        private static void WriteTypeExpression(BoundTypeExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Type.Name);
        }

        private static void WriteAssignmentExpression(BoundAssignmentExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Variable.Name);
            writer.WriteWhitespace();
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteWhitespace();
            node.Expression.WriteTo(writer);
        }

        private static void WriteBinaryExpression(BoundBinaryExpression node, IndentedTextWriter writer)
        {
            var precedence = SyntaxFacts.GetBinaryOperatorPrecedence(node.Op.SyntaxKind);

            writer.WriteNestedExpression(precedence, node.Left);
            writer.WriteWhitespace();
            writer.WritePunctuation(node.Op.SyntaxKind);
            writer.WriteWhitespace();
            writer.WriteNestedExpression(precedence, node.Right);
        }

        private static void WriteCallExpression(BoundCallExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Function.Name);
            writer.WritePunctuation(SyntaxKind.OpenParenthesesToken);  

            var isFirst = true;
            foreach (var arg in node.Arguments)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    writer.WritePunctuation(SyntaxKind.CommaToken);
                    writer.WriteWhitespace();
                }

                arg.WriteTo(writer);
            }

            writer.WritePunctuation(SyntaxKind.CloseParenthesesToken);   
        }

        private static void WriteReturnStatement(BoundReturnStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.ReturnKeyword);
            writer.WriteWhitespace();
            writer.Write(node.ReturnExpression);
            writer.WriteLine();
        }

        private static void WriteConversionExpression(BoundConversionExpression node, IndentedTextWriter writer)
        {
            var identifier = "to" +  char.ToUpper(node.Type.Name[0]) + node.Type.Name.Remove(0, 1);
            writer.WriteIdentifier(identifier);
            writer.WritePunctuation(SyntaxKind.OpenParenthesesToken);
            node.Expression.WriteTo(writer);
            writer.WritePunctuation(SyntaxKind.CloseParenthesesToken);

        }

        private static void WriteMemberAccessExpression(BoundMemberAccessExpression node, IndentedTextWriter writer)
        {
            writer.WriteIdentifier(node.Left.ToString());
            writer.WritePunctuation(SyntaxKind.FullStopToken);
            WriteTo(node.Right, writer);
        }

        private static void WriteBlockStatement(BoundBlockStatement node, IndentedTextWriter writer)
        {
                writer.WritePunctuation(SyntaxKind.OpenBraceToken);
                writer.WriteLine();
                writer.Indent++;

                foreach (var s in node.Statements)
                    s.WriteTo(writer);

                writer.Indent--;
                writer.WritePunctuation(SyntaxKind.CloseBraceToken);
                writer.WriteLine();
        }

        private static void WriteExpressionStatement(BoundExpressionStatement node, IndentedTextWriter writer)
        {
            node.Expression.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteVariableDeclaration(BoundVariableDeclaration node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(node.Variable.Type.ToString());
            writer.WriteWhitespace();
            writer.WriteIdentifier(node.Variable.Name);
            writer.WriteWhitespace();
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteWhitespace();
            node.Initializer.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteIfStatement(BoundIfStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.IfKeyword);
            writer.WriteWhitespace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.ThenStatement);
            if (node.ElseStatement != null)
            {
                writer.WriteKeyword(SyntaxKind.ElseKeyword);
                writer.WriteLine();
                writer.WriteNestedStatement(node.ElseStatement);
            }
        }

        private static void WriteWhileStatement(BoundWhileStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.WhileKeyword);
            writer.WriteWhitespace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);
        }

        private static void WriteDoWhileStatement(BoundDoWhileStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.DoKeyword);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);

            writer.WriteKeyword(SyntaxKind.WhileKeyword);
            writer.WriteWhitespace();
            node.Condition.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteForStatement(BoundForStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword(SyntaxKind.ForKeyword);
            writer.WriteWhitespace();
            writer.WriteIdentifier(node.Variable.Name);

            writer.WriteWhitespace();
            writer.WritePunctuation(SyntaxKind.EqualsToken);
            writer.WriteWhitespace();

            node.LowerBound.WriteTo(writer);

            writer.WriteWhitespace();
            writer.WriteKeyword(SyntaxKind.ToKeyword);
            writer.WriteWhitespace();

            node.UpperBound.WriteTo(writer);
            writer.WriteLine();
            writer.WriteNestedStatement(node.Body);
        }

        private static void WriteGotoStatement(BoundGotoStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword("goto");
            writer.WriteWhitespace();
            writer.WriteIdentifier(node.Label.Name);
            writer.WriteLine();
        }

        private static void WriteConditionalGotoStatement(BoundConditionalGotoStatement node, IndentedTextWriter writer)
        {
            writer.WriteKeyword("goto");
            writer.WriteWhitespace();
            
            writer.WriteIdentifier(node.Label.Name);

            writer.WriteIdentifier(node.Label.Name);
            writer.WriteKeyword(node.JumpIfTrue ? " if " : " unless ");
            writer.WriteIdentifier(node.Label.Name);

            node.Condition.WriteTo(writer);
            writer.WriteLine();
        }

        private static void WriteLabelStatement(BoundLabelStatement node, IndentedTextWriter writer)
        {
            var originalIndent = writer.Indent;
            writer.Indent = 0;

            writer.WritePunctuation(node.Label.Name);
            writer.WritePunctuation(SyntaxKind.ColonToken);

            writer.Indent = originalIndent;
            writer.WriteLine();
        }
    }
}