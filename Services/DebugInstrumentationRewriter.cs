using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class DebugInstrumentationRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel? _semanticModel;
    private HashSet<string> _declaredVariables = new();

    public DebugInstrumentationRewriter(SemanticModel? semanticModel = null)
    {
        _semanticModel = semanticModel;
    }

    public static SyntaxTree Instrument(SyntaxTree syntaxTree, SemanticModel? semanticModel = null)
    {
        var root = syntaxTree.GetRoot();
        var rewriter = new DebugInstrumentationRewriter(semanticModel);
        var newRoot = rewriter.Visit(root);
        return syntaxTree.WithChangedText(newRoot.GetText());
    }

    #region Scope Boundary Interception (Classes, Structs, Records, Interfaces)

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        VisitTypeDeclaration(node, () => base.VisitClassDeclaration(node));

    public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) =>
        VisitTypeDeclaration(node, () => base.VisitStructDeclaration(node));

    public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        VisitTypeDeclaration(node, () => base.VisitRecordDeclaration(node));

    public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) =>
        VisitTypeDeclaration(node, () => base.VisitInterfaceDeclaration(node));

    private SyntaxNode? VisitTypeDeclaration<T>(T node, Func<SyntaxNode?> visitBase) where T : SyntaxNode
    {
        // By C# language specification, types declared in a top-level file are separate sibling types.
        // Members of a type CANNOT access top-level local variables or local functions.
        // When entering a type, we isolate the scope completely (empty).
        var outerScope = _declaredVariables;
        _declaredVariables = new HashSet<string>();
        try
        {
            return visitBase();
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    #endregion

    #region Function & Member Boundaries (Methods, Constructors, Operators, Local Functions)

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var outerScope = _declaredVariables;
        var methodScope = new HashSet<string>(outerScope);

        // Parameters of the method are in scope
        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (!IsRefOrOutOrIn(param))
                {
                    methodScope.Add(param.Identifier.Text);
                }
            }
        }

        _declaredVariables = methodScope;
        try
        {
            return base.VisitMethodDeclaration(node);
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var outerScope = _declaredVariables;
        var ctorScope = new HashSet<string>(outerScope);

        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (!IsRefOrOutOrIn(param))
                {
                    ctorScope.Add(param.Identifier.Text);
                }
            }
        }

        _declaredVariables = ctorScope;
        try
        {
            return base.VisitConstructorDeclaration(node);
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        var outerScope = _declaredVariables;
        var opScope = new HashSet<string>(outerScope);

        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (!IsRefOrOutOrIn(param))
                {
                    opScope.Add(param.Identifier.Text);
                }
            }
        }

        _declaredVariables = opScope;
        try
        {
            return base.VisitOperatorDeclaration(node);
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        var outerScope = _declaredVariables;
        var opScope = new HashSet<string>(outerScope);

        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (!IsRefOrOutOrIn(param))
                {
                    opScope.Add(param.Identifier.Text);
                }
            }
        }

        _declaredVariables = opScope;
        try
        {
            return base.VisitConversionOperatorDeclaration(node);
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var outerScope = _declaredVariables;
        var isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);

        // A static local function cannot capture enclosing variables
        var localFuncScope = isStatic ? new HashSet<string>() : new HashSet<string>(outerScope);

        if (node.ParameterList != null)
        {
            foreach (var param in node.ParameterList.Parameters)
            {
                if (!IsRefOrOutOrIn(param))
                {
                    localFuncScope.Add(param.Identifier.Text);
                }
            }
        }

        _declaredVariables = localFuncScope;
        try
        {
            return base.VisitLocalFunctionStatement(node);
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    #endregion

    #region Statement & Block Rewriting

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var newStatements = new List<StatementSyntax>();

        // Enter a new lexical scope inheriting outer visible variables
        var outerScope = _declaredVariables;
        var localScopeVars = new HashSet<string>(outerScope);
        _declaredVariables = localScopeVars;
        try
        {
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
                    var probe = CreateProbeStatement(line, localScopeVars, statement);
                    newStatements.Add(probe);
                }

                // If this statement introduces local variables, record them for subsequent statements
                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    if (!localDecl.Modifiers.Any(SyntaxKind.RefKeyword))
                    {
                        var typeText = localDecl.Declaration.Type.ToString();
                        if (!typeText.StartsWith("Span<") && !typeText.StartsWith("ReadOnlySpan<") &&
                            typeText != "Span" && typeText != "ReadOnlySpan")
                        {
                            foreach (var v in localDecl.Declaration.Variables)
                            {
                                if (v.Initializer != null && v.Initializer.Value is not StackAllocArrayCreationExpressionSyntax)
                                {
                                    localScopeVars.Add(v.Identifier.Text);
                                }
                            }
                        }
                    }
                }

                // Recurse into nested structures (nested blocks, loops, conditionals)
                var visited = (StatementSyntax)Visit(statement);
                newStatements.Add(visited);
            }

            return node.WithStatements(SyntaxFactory.List(newStatements));
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var newMembers = new List<MemberDeclarationSyntax>();

        foreach (var member in node.Members)
        {
            if (member is GlobalStatementSyntax globalStatement)
            {
                var statement = globalStatement.Statement;
                var line = GetLineNumber(statement);

                if (line > 0 && !IsSyntheticProbe(statement))
                {
                    var probe = CreateProbeStatement(line, _declaredVariables, statement);
                    newMembers.Add(SyntaxFactory.GlobalStatement(probe));
                }

                if (statement is LocalDeclarationStatementSyntax localDecl)
                {
                    if (!localDecl.Modifiers.Any(SyntaxKind.RefKeyword))
                    {
                        var typeText = localDecl.Declaration.Type.ToString();
                        if (!typeText.StartsWith("Span<") && !typeText.StartsWith("ReadOnlySpan<") &&
                            typeText != "Span" && typeText != "ReadOnlySpan")
                        {
                            foreach (var v in localDecl.Declaration.Variables)
                            {
                                if (v.Initializer != null && v.Initializer.Value is not StackAllocArrayCreationExpressionSyntax)
                                {
                                    _declaredVariables.Add(v.Identifier.Text);
                                }
                            }
                        }
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
            var probe = CreateProbeStatement(line, _declaredVariables, node.Statement);
            visitedStatement = SyntaxFactory.Block(probe, visitedStatement);
        }

        ElseClauseSyntax? elseClause = null;
        if (node.Else != null)
        {
            var visitedElse = (StatementSyntax)Visit(node.Else.Statement);
            if (visitedElse is not BlockSyntax && visitedElse is not IfStatementSyntax)
            {
                var line = GetLineNumber(node.Else.Statement);
                var probe = CreateProbeStatement(line, _declaredVariables, node.Else.Statement);
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
            var probe = CreateProbeStatement(line, _declaredVariables, node.Statement);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var outerScope = _declaredVariables;
        var varsInLoop = new HashSet<string>(outerScope);
        if (node.Declaration != null)
        {
            foreach (var v in node.Declaration.Variables)
            {
                varsInLoop.Add(v.Identifier.Text);
            }
        }

        _declaredVariables = varsInLoop;
        StatementSyntax visited;
        try
        {
            visited = (StatementSyntax)Visit(node.Statement);
        }
        finally
        {
            _declaredVariables = outerScope;
        }

        if (visited is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, varsInLoop, node.Statement);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var outerScope = _declaredVariables;
        var varsInLoop = new HashSet<string>(outerScope)
        {
            node.Identifier.Text
        };

        _declaredVariables = varsInLoop;
        StatementSyntax visited;
        try
        {
            visited = (StatementSyntax)Visit(node.Statement);
        }
        finally
        {
            _declaredVariables = outerScope;
        }

        if (visited is not BlockSyntax)
        {
            var line = GetLineNumber(node.Statement);
            var probe = CreateProbeStatement(line, varsInLoop, node.Statement);
            visited = SyntaxFactory.Block(probe, visited);
        }
        return node.WithStatement(visited);
    }

    #endregion

    #region Probe Construction & Symbol Filtering

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

    private static bool IsRefOrOutOrIn(ParameterSyntax param)
    {
        return param.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.RefKeyword) ||
            m.IsKind(SyntaxKind.OutKeyword) ||
            m.IsKind(SyntaxKind.InKeyword));
    }

    private StatementSyntax CreateProbeStatement(int lineNumber, IEnumerable<string> variables, SyntaxNode? contextNode = null)
    {
        var candidates = variables
            .Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("_") && IsValidIdentifier(v))
            .Distinct()
            .ToList();

        var validVars = new List<string>();

        foreach (var v in candidates)
        {
            if (_semanticModel != null && contextNode != null)
            {
                try
                {
                    var symbols = _semanticModel.LookupSymbols(contextNode.SpanStart, name: v);
                    var sym = symbols.FirstOrDefault(s => s is ILocalSymbol or IParameterSymbol);
                    if (sym != null)
                    {
                        if (sym is ILocalSymbol local && (local.IsRef || local.Type.IsRefLikeType)) continue;
                        if (sym is IParameterSymbol param && (param.RefKind != RefKind.None || param.Type.IsRefLikeType)) continue;
                    }
                }
                catch
                {
                    // Proceed with candidate if symbol lookup throws
                }
            }

            validVars.Add(v);
            if (validVars.Count >= 30) break;
        }

        string probeCode;
        if (validVars.Count == 0)
        {
            probeCode = $"PdfEditorApp.Plugins.CSharpEditor.Services.ScriptDebugSession.Hit({lineNumber}, null);";
        }
        else
        {
            var entries = string.Join(", ", validVars.Select(v => $"{{ \"{v}\", (object?){v} }}"));
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

    #endregion
}
