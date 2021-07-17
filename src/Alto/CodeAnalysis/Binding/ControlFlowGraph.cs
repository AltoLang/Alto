using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Alto.CodeAnalysis.Symbols;
using Alto.CodeAnalysis.Syntax;

namespace Alto.CodeAnalysis.Binding
{
    internal sealed class ControlFlowGraph
    {
        private ControlFlowGraph(BasicBlock start, BasicBlock end, List<BasicBlock> blocks, List<BasicBlockBranch> branches)
        {
            Start = start;
            End = end;
            Blocks = blocks;
            Branches = branches;
        }

        public BasicBlock Start { get; }
        public BasicBlock End { get; }
        public List<BasicBlock> Blocks { get; }
        public List<BasicBlockBranch> Branches { get; }

        public void WriteTo(TextWriter writer)
        {
            writer.WriteLine("digraph G {");
            
            var blockIds = new Dictionary<BasicBlock, string>();
            for (int i = 0; i < Blocks.Count; i++)
            {
                var block = Blocks[i];
                var id = "N" + i.ToString();
                blockIds.Add(block, id);
            }

            foreach (var block in Blocks)
            {
                var id = blockIds[block];
                var label = block.ToString().Replace(System.Environment.NewLine, "\\l");
                writer.WriteLine($"    {id} [label = \"{label}\" shape = box]");
            }

            foreach (var branch in Branches)
            {
                var fromId = blockIds[branch.From];
                var toId = blockIds[branch.To];
                var label = branch.Condition == null ? string.Empty : branch.Condition.ToString();
                writer.WriteLine($"    {fromId} -> {toId} [label = \"{label}\"]");
            }

            writer.WriteLine("}");
        }
    
        public static ControlFlowGraph Create(BoundBlockStatement body)
        {
            var blockBuilder = new BasicBlockBuilder();
            var blocks = blockBuilder.Build(body);

            var graphBuilder = new GraphBuilder();
            return graphBuilder.Build(blocks);
        }

        public static bool AllPathsReturn(BoundBlockStatement body)
        {
            var graph = Create(body);

            foreach (var branch in graph.End.Incoming)
            {
                var stmt = branch.From.Statements.LastOrDefault();
                if (stmt == null || stmt.Kind != BoundNodeKind.ReturnStatement)
                    return false;
            }

            return true;
        }

        public sealed class BasicBlockBuilder
        {
            private List<BasicBlock> _blocks = new List<BasicBlock>();
            private List<BoundStatement> _statements = new List<BoundStatement>();

            public List<BasicBlock> Build(BoundBlockStatement block)
            {
                foreach (var statement in block.Statements)
                {
                    switch (statement.Kind)
                    {
                        case BoundNodeKind.ExpressionStatement:
                        case BoundNodeKind.VariableDeclaration:
                            _statements.Add(statement);
                            break;
                        case BoundNodeKind.GotoStatement:
                        case BoundNodeKind.ConditionalGotoStatement:
                        case BoundNodeKind.ReturnStatement:
                            _statements.Add(statement);
                            StartBlock();
                            break;
                        case BoundNodeKind.LabelStatement:
                            StartBlock();
                            _statements.Add(statement);
                            break;
                        default:
                            throw new Exception($"Unexpected statement {statement.Kind}");
                    }
                }

                EndBlock();
                
                return _blocks;
            }

            private void StartBlock()
            {
                EndBlock();
            }

            private void EndBlock()
            {
                if (_statements.Count > 0)
                {
                    var block = new BasicBlock();
                    block.Statements.AddRange(_statements);
                    _blocks.Add(block);
                    _statements.Clear();
                }
            }
        }

        public sealed class GraphBuilder
        {
            private Dictionary<BoundStatement, BasicBlock> _blockFromStatement = new Dictionary<BoundStatement, BasicBlock>();
            private Dictionary<BoundLabel, BasicBlock> _blockFromLabel = new Dictionary<BoundLabel, BasicBlock>();
            private List<BasicBlockBranch> _branches = new List<BasicBlockBranch>();
            private BasicBlock _start = new BasicBlock(isStart: true);
            private BasicBlock _end = new BasicBlock(isStart: false);

            public ControlFlowGraph Build(List<BasicBlock> blocks)
            {   
                if (!blocks.Any())
                    Connect(_start, _end);
                else
                    Connect(_start, blocks.First());

                foreach (var block in blocks)
                {
                    foreach (var statement in block.Statements)
                    {
                        _blockFromStatement.Add(statement, block);
                        if (statement is BoundLabelStatement ls)
                            _blockFromLabel.Add(ls.Label, block);
                    }
                }

                for (int i = 0; i < blocks.Count; i++)
                {
                    var current = blocks[i];
                    var next = i == blocks.Count - 1 ? _end : blocks[i + 1];
                    foreach (var statement in current.Statements)
                    {
                        var isLast = statement == current.Statements.Last();
                        Walk(statement, current, next, isLast);
                    }
                }
                
            Scan:
                foreach (var block in blocks)
                {
                    if (!block.Incoming.Any())
                    {
                        RemoveBlock(blocks, block);
                        goto Scan;
                    }
                }

                blocks.Insert(0, _start);
                blocks.Add(_end);

                return new ControlFlowGraph(_start, _end, blocks, _branches);
            }

            private void Walk(BoundStatement statement, BasicBlock current, BasicBlock next, bool isLast)
            {
                switch (statement.Kind)
                {
                    case BoundNodeKind.ExpressionStatement:
                    case BoundNodeKind.LabelStatement:
                    case BoundNodeKind.VariableDeclaration:
                        if (isLast)
                            Connect(current, next);
                        break;
                    case BoundNodeKind.GotoStatement:
                        var GotoStatement = (BoundGotoStatement)statement;
                        var toBlock = _blockFromLabel[GotoStatement.Label];
                        Connect(current, toBlock);
                        break;
                    case BoundNodeKind.ConditionalGotoStatement:
                        var conditionalGoto = (BoundConditionalGotoStatement)statement;
                        var thenBlock = _blockFromLabel[conditionalGoto.Label];
                        var elseBlock = next;
                        var negCondition = Negate(conditionalGoto.Condition);
                        var thenCondition = conditionalGoto.JumpIfTrue ? conditionalGoto.Condition : negCondition;
                        var elseCondition = conditionalGoto.JumpIfTrue ? negCondition : conditionalGoto.Condition;
                        Connect(current, thenBlock, condition: thenCondition);
                        Connect(current, elseBlock, condition: elseCondition);
                        break;
                    case BoundNodeKind.ReturnStatement:
                        Connect(current, _end);
                        break;
                    default:
                        throw new Exception($"Unexpected statement {statement.Kind}");
                }
            }

            private void Connect(BasicBlock from, BasicBlock to, BoundExpression condition = null)
            {
                if (condition is BoundLiteralExpression l)
                {
                    var v = (bool)l.Value;
                    if (v)
                        condition = null;
                    else
                        return;
                }

                var branch = new BasicBlockBranch(from, to, null);
                from.Outgoing.Add(branch);
                to.Incoming.Add(branch);
                _branches.Add(branch);
            }

            private void RemoveBlock(List<BasicBlock> blocks, BasicBlock block)
            {
                foreach (var branch in block.Incoming)
                {
                    branch.From.Outgoing.Remove(branch);
                    _branches.Remove(branch);
                }

                foreach (var branch in block.Outgoing)
                {
                    branch.To.Incoming.Remove(branch);
                    _branches.Remove(branch);
                }

                blocks.Remove(block);
            }

            private BoundExpression Negate(BoundExpression expression)
            {
                if (expression is BoundLiteralExpression literal)
                {
                    var value = (bool)literal.Value;
                    return new BoundLiteralExpression(!value);
                }

                var unaryOp = BoundUnaryOperator.Bind(SyntaxKind.BangToken, TypeSymbol.Bool);
                return new BoundUnaryExpression(unaryOp, expression);
            }
        }

        public sealed class BasicBlock
        {
            public BasicBlock()
            {
            }

            public BasicBlock(bool isStart)
            {
                IsStart = isStart;
                IsEnd = !isStart;
            }

            public List<BoundStatement> Statements { get; } = new List<BoundStatement>();
            public List<BasicBlockBranch> Incoming { get; } = new List<BasicBlockBranch>();
            public List<BasicBlockBranch> Outgoing { get; } = new List<BasicBlockBranch>();
            public bool IsStart { get; }
            public bool IsEnd { get; }

            public override string ToString()
            {
                if (IsStart)
                    return "<Start>";
                else if (IsEnd)
                    return "<End>";
                
                using (var w = new StringWriter())
                {
                    foreach (var statement in Statements)
                        statement.WriteTo(w);

                    return w.ToString();
                }
            }
        }

        public sealed class BasicBlockBranch
        {
            public BasicBlockBranch(BasicBlock from, BasicBlock to, BoundExpression condition)
            {
                From = from;
                To = to;
                Condition = condition;
            }

            public BasicBlock From { get; }
            public BasicBlock To { get; }
            public BoundExpression Condition { get; }
            public override string ToString()
            {
                if (Condition == null)
                    return string.Empty;

                return Condition.ToString();
            }
        }
    }
}