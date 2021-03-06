using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Rustic;
using Rustic.Source;

namespace LoggerMessage.Gen;

internal readonly struct GenContext
{
    public readonly NamespaceDeclarationSyntax Ns;
    public readonly ImmutableArray<BaseTypeDeclarationSyntax> Nesting;
    public readonly BaseTypeDeclarationSyntax TypeDecl;
    public readonly ImmutableArray<LoggerMessageContext> LoggerMessages;

    public readonly string Namespace;
    public readonly string Modifiers;
    public readonly string Symbol;

    public GenContext(NamespaceDeclarationSyntax ns, ImmutableArray<BaseTypeDeclarationSyntax> nesting, BaseTypeDeclarationSyntax typeDecl, ImmutableArray<LoggerMessageContext> loggerMessages)
    {
        Ns = ns;
        Nesting = nesting;
        TypeDecl = typeDecl;
        LoggerMessages = loggerMessages;

        Namespace = Ns.Name.ToString();
        Modifiers = TypeDecl.Modifiers.ToString();
        Symbol = TypeDecl.Identifier.Text;
    }

    public static GenContext? CollectDeclInfo(SynModel ctx)
    {
        if (!ctx.Is<BaseTypeDeclarationSyntax>(out var model))
        {
            return default;
        }
        var typeDecl = model.Node;
        var (nsDecl, nestingDecls) = typeDecl.GetHierarchy<BaseTypeDeclarationSyntax>();

        var loggerMessages = model.FilterTypeAttr(static (m, _) => m.GetTypeName() == Const.LoggerMessageSymbol)
            .Select(s => LoggerMessageContext.CollectAttrInfo(s, model))
            .NotNull()
            .ToImmutableArray();
        if (loggerMessages.IsDefaultOrEmpty)
        {
            return default;
        }

        return new GenContext(nsDecl, nestingDecls, typeDecl, loggerMessages);
    }
}

internal readonly struct LoggerMessageContext
{
    // https://regex101.com/r/OYnucB
    public static readonly Regex formatArgumentsRegex = new("(?<!(?:^|[^{]){(?:{{)*){([^{}]+):([^{}]+)}", RegexOptions.Compiled | RegexOptions.Singleline);

    public readonly AttributeSyntax Attr;
    public readonly string Name;
    public readonly string? EventId;
    public readonly string Format;
    public readonly ImmutableArray<(string, INamedTypeSymbol)> Arguments;
    public readonly MemberAccessExpressionSyntax? LogLevel;
    public readonly MemberAccessExpressionSyntax? ExtensionScope;

    public readonly string DelegateSymbolName;
    public readonly bool IsLoggerGeneric;
    public readonly string LoggerSymbol;

    public LoggerMessageContext(AttributeSyntax attr, string name, string format, string? eventId, ImmutableArray<(string, INamedTypeSymbol)> arguments, MemberAccessExpressionSyntax? logLevel, MemberAccessExpressionSyntax? extensionScope)
    {
        Attr = attr;
        Name = name;
        EventId = eventId;
        Format = format;
        Arguments = arguments;
        LogLevel = logLevel;
        ExtensionScope = extensionScope;

        DelegateSymbolName = $"s_{name}";
        IsLoggerGeneric = extensionScope is null || extensionScope.Name.Identifier.ValueText == "Generic";
        LoggerSymbol = IsLoggerGeneric ? "Microsoft.Extensions.Logging.ILogger<T>" : "Microsoft.Extensions.Logging.ILogger";
    }

    public static ReadOnlySpan<string> ArgList => new[] {
        "name",
        "logLevel",
        "format",
        "eventId",
        "extensionScope",
     };

    public static LoggerMessageContext? CollectAttrInfo(AttributeSyntax attr, SynModel<BaseTypeDeclarationSyntax> model)
    {
        if (attr.ArgumentList is null)
        {
            return default;
        }
        string? name = null;
        MemberAccessExpressionSyntax? logLevel = null;
        string? format = null;
        string? eventId = null;
        ImmutableArray<(string, INamedTypeSymbol)> arguments = default;
        MemberAccessExpressionSyntax? extensionScope = null;

        foreach ((AttributeArgumentSyntax arg, int index) in attr.ArgumentList.Arguments.WithIndex())
        {
            switch (arg.NameColon is null ? index : ArgList.IndexOf(arg.NameColon.Name.Identifier.ValueText))
            {
                case 0:
                    Debug.Assert(arg.Expression.Kind() == SyntaxKind.StringLiteralExpression);
                    name = ((LiteralExpressionSyntax)arg.Expression).Token.ToString();
                    break;
                case 1:
                    Debug.Assert(arg.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression);
                    logLevel = (MemberAccessExpressionSyntax)arg.Expression;
                    break;
                case 2:
                    Debug.Assert(arg.Expression.Kind() == SyntaxKind.StringLiteralExpression);
                    (format, arguments) = ParseFormatSyntax(model, arg);
                    break;
                case 3:
                    Debug.Assert(arg.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression);
                    eventId = ((LiteralExpressionSyntax)arg.Expression).Token.ToString();
                    break;
                case 4:
                    Debug.Assert(arg.Expression.Kind() == SyntaxKind.SimpleMemberAccessExpression);
                    extensionScope = (MemberAccessExpressionSyntax)arg.Expression;
                    break;
                default:
                    throw null!;
            }
        }

        Debug.Assert(!name.IsEmpty());
        Debug.Assert(!format.IsEmpty());
        return new LoggerMessageContext(attr, name!, format!, eventId, arguments, logLevel, extensionScope);
    }

    private static (string, ImmutableArray<(string, INamedTypeSymbol)>) ParseFormatSyntax(SynModel<BaseTypeDeclarationSyntax> model, AttributeArgumentSyntax arg)
    {
        var expr = (LiteralExpressionSyntax)arg.Expression;
        var template = expr.Token.ToString();
        MatchCollection matches = formatArgumentsRegex.Matches(template);

        Debug.Assert(matches.Count > 0);
        StrBuilder fmt = new(stackalloc char[template.Length]);
        var argsBuilder = ImmutableArray.CreateBuilder<(string, INamedTypeSymbol)>(matches.Count);
        int templateIdx = 0;

        foreach (Match match in matches)
        {
            Group alias = match.Groups[1];
            Group type = match.Groups[2];

            var types = model.Model.LookupNamespacesAndTypes(model.Node.Identifier.FullSpan.Start, name: alias.Value)
                .OfType<INamedTypeSymbol>()
                .Where(static (s) => s.CanBeReferencedByName && s.IsType)
                .ToImmutableArray();
            Debug.Assert(types.Length != 1);
            argsBuilder.Add((alias.Value, types[0]));

            Debug.Assert(match.Index >= templateIdx);
            fmt.Append(template.AsSpan(templateIdx, templateIdx - match.Index));
            fmt.Append('{');
            fmt.Append(alias.Value);
            fmt.Append('}');
            templateIdx = match.Index + match.Length;
        }
        var arguments = argsBuilder.MoveToImmutable();

        fmt.Append(template.AsSpan(templateIdx));
        var format = fmt.ToString();
        return (format, arguments);
    }
}
