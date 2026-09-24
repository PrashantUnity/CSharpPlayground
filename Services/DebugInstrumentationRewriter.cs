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
    private int _frameCounter;

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
            var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            return visited.Body is { } body && !ContainsYield(node.Body)
                ? visited.WithBody(WithFrameScope(body, node))
                : visited;
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
            var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            return visited.Body is { } body ? visited.WithBody(WithFrameScope(body, node)) : visited;
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
            var visited = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
            return visited.Body is { } body ? visited.WithBody(WithFrameScope(body, node)) : visited;
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
            var visited = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
            return visited.Body is { } body ? visited.WithBody(WithFrameScope(body, node)) : visited;
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var outerScope = _declaredVariables;

        // Capture only enclosing locals the function reads; others would break `int total = Depth(3);` above `Depth`.
        var localFuncScope = EnclosingLocalsReadBy(node, outerScope);

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
            var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
            return visited.Body is { } body && !ContainsYield(node.Body)
                ? visited.WithBody(WithFrameScope(body, node))
                : visited;
        }
        finally
        {
            _declaredVariables = outerScope;
        }
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var visited = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        return visited.Body is { } body && !ContainsYield(node.Body)
            ? visited.WithBody(WithFrameScope(body, node))
            : visited;
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        var visited = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;
        return visited.Block is { } block ? visited.WithBlock(WithFrameScope(block, node)) : visited;
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var visited = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
        return visited.Block is { } block ? visited.WithBlock(WithFrameScope(block, node)) : visited;
    }

    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        var visited = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return visited.WithBlock(WithFrameScope(visited.Block, node));
    }

    #endregion

    private HashSet<string> EnclosingLocalsReadBy(LocalFunctionStatementSyntax node, HashSet<string> outerScope)
    {
        var names = new HashSet<string>();
        if (_semanticModel == null || node.Modifiers.Any(SyntaxKind.StaticKeyword) || outerScope.Count == 0)
        {
            return names;
        }

        try
        {
            var flow = node.Body != null
                ? _semanticModel.AnalyzeDataFlow(node.Body)
                : node.ExpressionBody != null ? _semanticModel.AnalyzeDataFlow(node.ExpressionBody.Expression) : null;
            if (flow is not { Succeeded: true })
            {
                return names;
            }

            foreach (var symbol in flow.ReadInside.OfType<ILocalSymbol>())
            {
                bool declaredOutside = symbol.DeclaringSyntaxReferences.All(r => !node.Span.Contains(r.Span));
                if (declaredOutside && outerScope.Contains(symbol.Name))
                {
                    names.Add(symbol.Name);
                }
            }
        }
        catch
        {
            // Without flow analysis, capture nothing rather than risk unassigned-variable errors
        }

        return names;
    }

    #region Call Stack Frames

    private BlockSyntax WithFrameScope(BlockSyntax body, SyntaxNode declaration)
    {
        var name = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(DescribeFrame(declaration), quote: true);
        var next = body.Statements.Count > 0 ? body.Statements[0].GetLeadingTrivia() : body.CloseBraceToken.LeadingTrivia;
        var frameStatement = SyntaxFactory.ParseStatement(
                $"using var __dbgFrame{_frameCounter++} = PdfEditorApp.Plugins.CSharpEditor.Services.ScriptDebugSession.EnterFrame({name}, {GetLineNumber(declaration)});")
            .WithTrailingTrivia(SeparatorBefore(next));

        return body.WithStatements(body.Statements.Insert(0, frameStatement));
    }

    // Iterator bodies run lazily across MoveNext calls, so a frame pushed at entry would outlive each yield.
    private static bool ContainsYield(SyntaxNode? body) =>
        body != null &&
        body.DescendantNodes(n => n == body || n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
            .OfType<YieldStatementSyntax>()
            .Any();

    private static string DescribeFrame(SyntaxNode declaration) => declaration switch
    {
        MethodDeclarationSyntax m => WithTypePrefix(m, $"{m.Identifier.Text}{m.TypeParameterList}{FormatParameters(m.ParameterList)}"),
        ConstructorDeclarationSyntax c => WithTypePrefix(c, $"{c.Identifier.Text}{FormatParameters(c.ParameterList)}"),
        OperatorDeclarationSyntax o => WithTypePrefix(o, $"operator {o.OperatorToken.Text}{FormatParameters(o.ParameterList)}"),
        ConversionOperatorDeclarationSyntax co => WithTypePrefix(co, $"{co.ImplicitOrExplicitKeyword.Text} operator {co.Type}{FormatParameters(co.ParameterList)}"),
        LocalFunctionStatementSyntax lf => $"{lf.Identifier.Text}{lf.TypeParameterList}{FormatParameters(lf.ParameterList)}",
        AccessorDeclarationSyntax a => WithTypePrefix(a, $"{DescribeAccessorOwner(a)}.{a.Keyword.Text}"),
        ParenthesizedLambdaExpressionSyntax pl => $"<lambda>{FormatParameters(pl.ParameterList)}",
        SimpleLambdaExpressionSyntax sl => $"<lambda>({sl.Parameter.Identifier.Text})",
        _ => "<anonymous method>"
    };

    private static string DescribeAccessorOwner(AccessorDeclarationSyntax accessor) => accessor.Parent?.Parent switch
    {
        PropertyDeclarationSyntax property => property.Identifier.Text,
        IndexerDeclarationSyntax indexer => $"this{FormatParameters(indexer.ParameterList)}",
        EventDeclarationSyntax evt => evt.Identifier.Text,
        _ => "accessor"
    };

    private static string WithTypePrefix(SyntaxNode node, string name)
    {
        var containingType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return containingType == null ? name : $"{containingType.Identifier.Text}.{name}";
    }

    // Called statically, as elsewhere in the plugin: Rider can't resolve the extension-method form.
    private static string FormatParameters(BaseParameterListSyntax parameters) =>
        Microsoft.CodeAnalysis.SyntaxNodeExtensions.NormalizeWhitespace(parameters).ToString();

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

                // Recurse into nested structures (nested blocks, loops, conditionals)
                var visited = (StatementSyntax)Visit(statement);
                newStatements.Add(visited);

                // Record declared locals only after visiting, so a lambda in their own initializer cannot capture them
                RegisterDeclaredVariables(statement, localScopeVars);
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

                var visitedStatement = (StatementSyntax)Visit(statement);
                newMembers.Add(SyntaxFactory.GlobalStatement(visitedStatement));
                RegisterDeclaredVariables(statement, _declaredVariables);
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

    private static void RegisterDeclaredVariables(StatementSyntax statement, HashSet<string> scope)
    {
        if (statement is not LocalDeclarationStatementSyntax localDecl || localDecl.Modifiers.Any(SyntaxKind.RefKeyword))
        {
            return;
        }

        var typeText = localDecl.Declaration.Type.ToString();
        if (typeText.StartsWith("Span<") || typeText.StartsWith("ReadOnlySpan<") || typeText is "Span" or "ReadOnlySpan")
        {
            return;
        }

        foreach (var v in localDecl.Declaration.Variables)
        {
            if (v.Initializer != null && v.Initializer.Value is not StackAllocArrayCreationExpressionSyntax)
            {
                scope.Add(v.Identifier.Text);
            }
        }
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
            .WithTrailingTrivia(contextNode == null ? SyntaxFactory.CarriageReturnLineFeed : SeparatorBefore(contextNode.GetLeadingTrivia()));
    }

    // Inserted code shares the next statement's line so original line numbers survive; directives need a line start.
    private static SyntaxTrivia SeparatorBefore(SyntaxTriviaList nextLeadingTrivia) =>
        nextLeadingTrivia.Any(t => t.IsDirective) ? SyntaxFactory.CarriageReturnLineFeed : SyntaxFactory.Space;

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    #endregion
}
