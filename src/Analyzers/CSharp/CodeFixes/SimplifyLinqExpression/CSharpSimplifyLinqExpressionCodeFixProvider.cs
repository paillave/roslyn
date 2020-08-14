﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyLinqExpression
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    internal sealed class CSharpSimplifyLinqExpressionCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public CSharpSimplifyLinqExpressionCodeFixProvider()
        {
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds
           => ImmutableArray.Create(IDEDiagnosticIds.SimplifyLinqExpressionsDiagnosticId);

        internal override CodeFixCategory CodeFixCategory => CodeFixCategory.CodeQuality;

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            context.RegisterCodeFix(new MyCodeAction(
                c => FixAsync(context.Document, context.Diagnostics.First(), c)), context.Diagnostics);

            return Task.CompletedTask;
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            //var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            foreach (var diagnostic in diagnostics)
            {
                var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                RemoveWhere(editor, node, diagnostic.AdditionalLocations);
            }
            return Task.CompletedTask;
        }

        private static void RemoveWhere(SyntaxEditor editor, SyntaxNode node, IReadOnlyList<Location> additionalLocations)
        {
            var additionalNodes = new List<SyntaxNode>();
            foreach (var locations in additionalLocations)
            {
                additionalNodes.Add(editor.OriginalRoot.FindNode(locations.SourceSpan, getInnermostNodeForTie: true));
            }

            var expressionNode = ((InvocationExpressionSyntax)node).Expression;

            // Get the Linq expression being invoked
            // Example: 'Single' from 'Data.Where(x => x == 1).Single()'
            var targetMethod = additionalNodes.OfType<IdentifierNameSyntax>().Last();
            var objectNodeSyntax = additionalNodes.First();

            // Retrieve the lambda expression from the node
            // Example: 'x => x == 1' from 'Data.Where(x => x == 1).Single()'
            var arguments = additionalNodes.OfType<ArgumentListSyntax>().First();

            var expression = GetExpression(objectNodeSyntax, arguments);

            var newNode = SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    expression,
                                    targetMethod))
                            .WithArgumentList(arguments);
            editor.ReplaceNode(expressionNode.Parent!, newNode);
        }

        private static ExpressionSyntax GetExpression(SyntaxNode objectNodeSyntax, SyntaxNode arguments)
        {
            if (((ArgumentListSyntax)arguments).Arguments.Count > 1)
            {
                return SyntaxFactory.IdentifierName("Enumerable");
            }

            // Get the data or object the query is being called on
            // Example: 'Data' from 'Data.Where(x => x == 1).Single()'
            return objectNodeSyntax.IsKind(SyntaxKind.InvocationExpression) ||
                   objectNodeSyntax.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                ? (ExpressionSyntax)objectNodeSyntax
                : SyntaxFactory.IdentifierName(((IdentifierNameSyntax)objectNodeSyntax).Identifier.Text);
        }

        private class MyCodeAction : CustomCodeActions.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(CSharpAnalyzersResources.Simplify_Linq_expression, createChangedDocument, CSharpAnalyzersResources.Simplify_Linq_expression)
            {
            }
        }
    }
}
