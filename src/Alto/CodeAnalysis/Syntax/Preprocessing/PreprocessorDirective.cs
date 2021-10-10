using System.Collections.Generic;

namespace Alto.CodeAnalysis.Syntax.Preprocessing
{
    class PreprocessorDirective : MemberSyntax
    {
        public PreprocessorDirective(SyntaxTree syntaxTree, DirectiveKind? directiveKind, List<SyntaxToken> identifiers)
            : base(syntaxTree)
        {
            DirectiveKind = directiveKind;
            Identifiers = identifiers;
        }

        public DirectiveKind? DirectiveKind { get; }
        public List<SyntaxToken> Identifiers { get; }
        public override SyntaxKind Kind => SyntaxKind.PreprocessorDirective;
    }
}