using System;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Rustic.Source;

namespace LoggerMessage.Gen;

[Generator]
[CLSCompliant(false)]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static (ctx) =>
            ctx.AddSource($"{Const.LoggerMessageSymbol}.g.cs", SourceText.From(Const.LoggerMessageSyntax, Encoding.UTF8)));

        var typeDecls = context.SyntaxProvider.CreateSyntaxProvider(
                static (s, _) => s is BaseTypeDeclarationSyntax,
                static (ctx, _) => GenContext.CollectDeclInfo(ctx))
                .NotNull();

        context.RegisterSourceOutput(typeDecls.Collect(), static (ctx, src) => Generate(ctx, src));
    }

    private static void Generate(SourceProductionContext context, ImmutableArray<GenContext> declsCtx)
    {
        if (declsCtx.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var ctx in declsCtx)
        {
            SrcBuilder text = new(2048);
            Generate(text, in ctx);
            context.AddSource($"{ctx.Symbol}LoggerMessages.g.cs", SourceText.From(text.ToString(), Encoding.UTF8));
        }
    }

    private static void Generate(SrcBuilder text, in GenContext ctx)
    {
        LoggerMessageGen gen = new(text);

        using (text.NullableEnable())
        {
            text.Stmt("using System;")
                .Stmt("using System.ComponentModel;")
                .Stmt("using System.Collections.Generic;")
                .Stmt("using System.Runtime.CompilerServices;")
                .Stmt("using System.Runtime.Serialization;")
                .Stmt("using System.Runtime.InteropServices;")
                .NL()
                .Stmt("using LoggerMessage.Gen;")
                .NL()
                .NL();

            using (text.Decl($"namespace {ctx.Namespace}"))
            {
                gen.Generate(in ctx);
            }
        }
    }
}

internal static class Const
{
    public const string LoggerMessageSymbol = "LoggerMessage.Gen.LoggerMessageAttribute";
    public const string LoggerMessageSyntax = @"// Generated using https://github.com/ProphetLamb/LoggerMessageGen.
// Copyright (c) 2022 ProphetLamb, licensed under the MIT license

#nullable enable
namespace LoggerMessage.Gen
{
    /// <summary>
    /// Specifies to what <see cref=""Microsoft.Extensions.Logging.ILogger""/> types the message is available to.
    /// </summary>
    public enum Scope
    {
        /// <summary>
        /// The logger message is only available to generic the <see cref=""Microsoft.Extensions.Logging.ILogger{T}""/>
        /// with the category of the annotated type.
        /// </summary>
        Generic,
        /// <summary>
        /// The logger message is available to all <see cref=""Microsoft.Extensions.Logging.ILogger""/> implementations.
        /// </summary>
        Interface,
    }

    /// <summary>
    /// Defines a <see cref=""Microsoft.Extensions.Logging.LoggerMessage""/> and the associated extension method as defined in this attribute.
    /// </summary>
    /// <example>
    /// <c>
    /// [LoggerMessage(""LoginFailed"", LogLevel.Warning, ""Login failed. User = {User:string}, Host = {Host:string}, Attempt = {Attempt:int}."")]
    /// </c>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class LoggerMessageAttribute : Attribute
    {
        /// <summary>
        /// Defines a <see cref=""Microsoft.Extensions.Logging.LoggerMessage""/> and the associated extension method as defined in this attribute.
        /// </summary>
        /// <param name=""name"">The name of the message, event, and extension method. The extension method name is mingled with the log level.</param>
        /// <param name=""logLevel"">The log level of the message.</param>
        /// <param name=""format"">The format string contains parameters in the format `{Name:Type}`.</param>
        /// <param name=""eventId"">The <see cref=""Microsoft.Extensions.Logging.EventId""/>; if omitted, is generated based on the type, name and log level.</param>
        /// <param name=""options"">The <see cref=""Microsoft.Extensions.Logging.LogDefineOptions""/>.</param>
        /// <param name=""extensionScope"">The <see cref=""Scope""/>. Specifies to what <see cref=""""Microsoft.Extensions.Logging.ILogger""""/> types the message is available to.</param>
        /// <example>
        /// <c>
        /// [LoggerMessage(""LoginFailed"", LogLevel.Warning, ""Login failed. User = {User:string}, Host = {Host:string}, Attempt = {Attempt:int}.""]
        /// </c>
        /// </example>
        public LoggerMessageAttribute(string name, Microsoft.Extensions.Logging.LogLevel logLevel, string format, int eventId = 0, Scope extensionScope = Scope.Generic)
        {
            Name = name;
            LogLevel = logLevel;
            Format = format;
            EventId = eventId;
            ExtensionScope = extensionScope;
        }

        public string Name { get; }
        public int EventId { get; }
        public Microsoft.Extensions.Logging.LogLevel LogLevel { get; }
        public string Format { get; }
        public Scope ExtensionScope { get; }
    }
}
#nullable restore
";
}

internal readonly struct LoggerMessageGen
{
    internal readonly SrcBuilder Text;

    public LoggerMessageGen(SrcBuilder text)
    {
        Text = text;
    }

    public void Generate(in GenContext ctx)
    {
        using (Text.Decl($"{ctx.Modifiers} static class {ctx.Symbol}LoggerMessages"))
        {
            foreach (var attr in ctx.LoggerMessages)
            {
                Define(in ctx, attr);
            }
        }
    }

    private string Define(in GenContext ctx, in LoggerMessageContext attr)
    {
        return "";
    }

    private void Extension(in GenContext ctx, in LoggerMessageContext attr)
    {

    }
}
