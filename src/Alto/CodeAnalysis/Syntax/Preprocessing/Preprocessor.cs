using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Alto.CodeAnalysis.Text;

namespace Alto.CodeAnalysis.Syntax.Preprocessing
{
    class Preprocessor
    {
        private ImmutableArray<MemberSyntax> _members;
        private List<MemberSyntax> _newMembers;
        private DiagnosticBag _diagnostics = new DiagnosticBag();

        public Preprocessor(ImmutableArray<MemberSyntax> members)
        {
            _members = members;
            _newMembers = _members.ToList();
        }

        public DiagnosticBag Diagnostics => _diagnostics;

        public ImmutableArray<MemberSyntax> Process()
        {
            var directives = _members.OfType<PreprocessorDirective>();
            foreach (var directive in directives)
                EvaluateDirective(directive);

            return _newMembers.ToImmutableArray();
        }

        private void EvaluateDirective(PreprocessorDirective directive)
        {
            var d = directive.Identifiers[0];
            var kind = ClassifyDirective(d.Text);
            if (kind == null)
            {
                _diagnostics.ReportDirectiveExpected(d.Location);
                return;
            }

            switch (kind)
            {
                case DirectiveKind.UsingDirective:
                    break;
            }
        }

        public static DirectiveKind? ClassifyDirective(string text)
        {
            switch (text)
            {
                case "using":
                    return DirectiveKind.UsingDirective;
                default:
                    return null;
            }
        }
    }
}