using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class DebugInstrumentationRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _declaredVariables = new();

    public static SyntaxTree Instrument(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetRoot();
        var rewriter = new DebugInstrumentationRewriter();
        var newRoot = rewriter.Visit(root);
        return syntaxTree.WithChangedText(newRoot.GetText());
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var newStatements = new List<StatementSyntax>();
        var localScopeVars = new HashSet<string>(_declaredVariables);

        foreach (var statement in node.Statements)
        {
            // Do not instrument synthetic probes
            if (IsSyntheticProbe(statement))
            {
                newStatements.Add(statement);
                continue;
            }

            var line = GetLineNumber(statement);
            if (line > 0)
            {
                var probe = CreateProbeStatement(line, localScopeVars);
                newStatements.Add(probe);
            }

            // If this statement is a local declaration, record newly introduced variables for subsequent statements
            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var v in localDecl.Declaration.Variables)
                {
                    localScopeVars.Add(v.Identifier.Text);
                }
            }

            // Recurse into nested structures (blocks, loops, conditionals)
            var visited = (StatementSyntax)Visit(statement);
            newStatements.Add(visited);
        }

        return node.WithStatements(SyntaxFactory.List(newStatements));
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var newMembers = new List<MemberDeclarationSyntax>();
        var topLevelVars = new HashSet<string>();

        foreach (var member in node.Members)
        {
            if (member is GlobalStatementSyntax globalStatement)
            {
                var statement = globalStatement.Statement;
                var line = GetLineNumber(statement);

                if (line > 0 && !IsSyntheticProbe(statement))
                {
                    var probe = CreateProbeStatement(line, topLevelVars);
                    newMembers.Add(SyntaxFactory.GlobalStatement(probe));
                }

                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    foreach (var v in localDecl.Declaration.Variables)
                    {
                        topLevelVars.Add(v.Identifier.Text);
                    }
                }

                var visitedStatement = (StatementSyntax)Visit(statement);
                newMembers.Add(SyntaxFactory.GlobalStatement(visitedStatement));
            }
            else
            {
                var visitedMember = (MemberDeclarationSyntax)Visit(member);
                newMembers.Add(visitedMember);
            }
        }

        return node.WithMembers(SyntaxFactory.List(newMembers));
    }

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        var visitedStatement = (StatementSyntax)Visit(node.Statement);
        if (visitedStatement is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, _declaredVariables);
            visitedStatement = SyntaxFactory.Block(probe, visitedStatement);
        }

        ElseClauseSyntax? elseClause = null;
        if (node.Else != null)
        {
            var visitedElse = (StatementSyntax)Visit(node.Else.Statement);
            if (visitedElse is not BlockSyntax && visitedElse is not IfStatementSyntax)
            {
                var line = GetLineNumber(node.Else.Statement);
                var probe = CreateProbeStatement(line, _declaredVariables);
                visitedElse = SyntaxFactory.Block(probe, visitedElse);
            }
            elseClause = node.Else.WithStatement(visitedElse);
        }

        return node.WithStatement(visitedStatement).WithElse(elseClause);
    }

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var visited = (StatementSyntax)Visit(node.Statement);
        if (visited is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, _declaredVariables);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var varsInLoop = new HashSet<string>(_declaredVariables);
        if (node.Declaration != null)
        {
            foreach (var v in node.Declaration.Variables)
            {
                varsInLoop.Add(v.Identifier.Text);
            }
        }

        var visited = (StatementSyntax)Visit(node.Statement);
        if (visited is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, varsInLoop);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var varsInLoop = new HashSet<string>(_declaredVariables)
        {
            node.Identifier.Text
        };

        var visited = (StatementSyntax)Visit(node.Statement);
        if (visited is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, varsInLoop);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    private static int GetLineNumber(SyntaxNode node)
    {
        var mapped = node.GetLocation().GetMappedLineSpan();
        if (mapped.IsValid && (string.IsNullOrEmpty(mapped.Path) || mapped.Path == "script.cs"))
        {
            return mapped.StartLinePosition.Line + 1;
        }

        var span = node.GetLocation().GetLineSpan();
        return span.StartLinePosition.Line + 1;
    }

    private static bool IsSyntheticProbe(StatementSyntax statement)
    {
        return statement.ToFullString().Contains("ScriptDebugSession.Hit");
    }

    private static StatementSyntax CreateProbeStatement(int lineNumber, IEnumerable<string> variables)
    {
        var distinctVars = variables
            .Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("_") && IsValidIdentifier(v))
            .Distinct()
            .Take(30)
            .ToList();

        string probeCode;
        if (distinctVars.Count == 0)
        {
            probeCode = $"PdfEditorApp.Plugins.CSharpEditor.Services.ScriptDebugSession.Hit({lineNumber}, null);";
        }
        else
        {
            var entries = string.Join(", ", distinctVars.Select(v => $"{{ \"{v}\", (object?){v} }}"));
            probeCode = $"PdfEditorApp.Plugins.CSharpEditor.Services.ScriptDebugSession.Hit({lineNumber}, () => new System.Collections.Generic.Dictionary<string, object?> {{ {entries} }});";
        }

        return SyntaxFactory.ParseStatement(probeCode)
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
