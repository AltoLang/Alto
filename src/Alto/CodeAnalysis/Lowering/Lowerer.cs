using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alto.CodeAnalysis.Binding;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Lowering
{
    internal sealed class Lowerer : BoundTreeRewriter
    {
        private int _labelCount;

        private Lowerer()
        {
            
        }

        private BoundLabel GenerateLabel()
        {
            var name = $"Label{++_labelCount}";
            return new BoundLabel(name);
        }

        private BoundLabel GenerateLabel(string prefix)
        {
            var name = prefix + (++_labelCount).ToString();
            return new BoundLabel(name);
        }

        public static BoundBlockStatement Lower(BoundStatement statement)
        {
            var lowerer = new Lowerer();
            var result = lowerer.RewriteStatement(statement);
            var flat = Flatten(result);
            return flat;
        }

        private static BoundBlockStatement Flatten(BoundStatement statement)
        {
            var builder = ImmutableArray.CreateBuilder<BoundStatement>();
            var stack = new Stack<BoundStatement>();
            stack.Push(statement);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current is BoundBlockStatement block)
                    foreach (var s in block.Statements.Reverse())
                        stack.Push(s);
                
                else
                    builder.Add(current);
            }

            return new BoundBlockStatement(builder.ToImmutable());
        }

        protected override BoundStatement RewriteIfStatement(BoundIfStatement node)
        {
            if (node.ElseStatement == null)
            {
                // if (true == true)
                //     x = x + 1
                //
                // ----->
                //
                // gotoFalse (true == true) end
                // x = x + 1
                // end:

                var endLabel = GenerateLabel();
                var gotoFalse = new BoundConditionalGotoStatement(endLabel, node.Condition, false);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(gotoFalse, node.ThenStatement, endLabelStatement));

                return RewriteStatement(result);
            }
            else
            {
                // if (true == true)
                //     x = x + 1
                // else
                //     y = x
                //
                // ----->
                //
                // gotoFalse (true == true) else
                // x = x + 1
                // goto end
                // else:
                // y = x
                // end:

                var elseLabel = GenerateLabel();
                var endLabel = GenerateLabel();
                var gotoFalse = new BoundConditionalGotoStatement(elseLabel, node.Condition, false);
                var gotoEndStatement = new BoundGotoStatement(endLabel);
                var elseLabelStatement = new BoundLabelStatement(elseLabel);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    gotoFalse,
                    node.ThenStatement,
                    gotoEndStatement,
                    elseLabelStatement,
                    node.ElseStatement,
                    endLabelStatement
                ));

                return RewriteStatement(result);
            }
        }

        protected override BoundStatement RewriteWhileStatement(BoundWhileStatement node)
        {
            // while true
            //    x = x + 1
            //
            // ------->
            //
            // goto check
            // continue:
            //     x = x + 1
            // check:
            //     gotoFalse true end
            // break:

            var checkLabel = GenerateLabel();

            var gotoCheck = new BoundGotoStatement(checkLabel);
            var continueLabelStatement = new BoundLabelStatement(node.ContinueLabel);
            var checkLabelStatement = new BoundLabelStatement(checkLabel);
            var gotoTrue = new BoundConditionalGotoStatement(node.ContinueLabel, node.Condition, true);
            var breakLabelStatement = new BoundLabelStatement(node.BreakLabel);
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    gotoCheck, 
                    continueLabelStatement, 
                    node.Body,
                    checkLabelStatement, 
                    gotoTrue, 
                    breakLabelStatement
                )
            );

            return RewriteStatement(result);
        }

        protected override BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement node)
        {
            // do
            // {
            //      print("do while statement test")
            // }
            // while <condition>

            // --------->

            // goto check
            // continue:
            //      print("do while statement test")
            // check:
            //      gotoFalse <condition> end
            // break:

            var continueLabelStatement = new BoundLabelStatement(node.ContinueLabel);
            var gotoTrue = new BoundConditionalGotoStatement(node.ContinueLabel, node.Condition);
            var breakLabelStatement = new BoundLabelStatement(node.BreakLabel);

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    continueLabelStatement, 
                    node.Body,
                    gotoTrue, 
                    breakLabelStatement
                )
            );

            return RewriteStatement(result);
        }
        
        protected override BoundStatement RewriteForStatement(BoundForStatement node)
        {
            // for i = 0 to 10
            //    print i
            //
            // ----->
            //
            //{
            //     let upperBound = <upper>
            //     while (<var> <= upperBound)
            //     {
            //         print i
            //
            //         continue:
            //         i = i + 1
            //     }
            //}
            
            var variableDeclaration = new BoundVariableDeclaration(node.Variable, node.LowerBound);
            var variableExpression = new BoundVariableExpression(node.Variable);
            var upperBoundSymbol = new LocalVariableSymbol("upperBound", true, TypeSymbol.Int);
            var upperBoundDeclaration = new BoundVariableDeclaration(upperBoundSymbol, node.UpperBound);
            var condition = new BoundBinaryExpression(
                variableExpression,
                BoundBinaryOperator.Bind(SyntaxKind.LesserOrEqualsToken, TypeSymbol.Int, TypeSymbol.Int), 
                new BoundVariableExpression(upperBoundSymbol)
            );

            var continueLabelStatement = new BoundLabelStatement(node.ContinueLabel);
            var increment = new BoundExpressionStatement(
                new BoundAssignmentExpression(node.Variable, 
                    new BoundBinaryExpression(
                        variableExpression, 
                        BoundBinaryOperator.Bind(SyntaxKind.PlusToken, TypeSymbol.Int, TypeSymbol.Int),
                        new BoundLiteralExpression(1)
                    )
                )
            );
            
            var whileBody = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.Body, continueLabelStatement, increment));
            var whileStatement = new BoundWhileStatement(condition, whileBody, node.BreakLabel, GenerateLabel());
            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(variableDeclaration, upperBoundDeclaration, whileStatement));

            return RewriteStatement(result);
        }
    }
}