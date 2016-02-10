namespace Microsoft.DotNet.CodeFormatter.Analyzers
{
    public static class RuleType
    {
        /// <summary>
        /// This type of rule is a purely syntactic rule and doesn't need any semantic information to analyze or fix the issue.
        /// Such rules can be correct in a broken compilation as well.
        /// </summary>
        public const string Syntactic = nameof(Syntactic);

        /// <summary>
        /// This type of rule needs to look at semantic information to either diagnose or fix the issue. The semantic information
        /// is localized the site of the issue and any fix will only semantically affect the site of the issue or the enclosing code block 
        /// for example: an edit inside a method body.
        /// </summary>
        public const string LocalSemantic = nameof(LocalSemantic);

        /// <summary>
        /// This type of rule needs to look at semantic information to either diagnose or fix the issue. A fix for the rule cause a 
        /// global semantic change.
        /// </summary>
        public const string GlobalSemantic = nameof(GlobalSemantic);
    }
}