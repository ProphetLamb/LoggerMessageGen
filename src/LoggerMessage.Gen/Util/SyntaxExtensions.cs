// Source https://github.com/ProphetLamb/rustic-sharp/tree/master/src/Rustic.Source

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rustic.Source;

[CLSCompliant(false)]
public static class SyntaxExtensions
{
    public static WithIndexIterator<T> WithIndex<T>(this IEnumerable<T> sequence)
    {
        return new WithIndexIterator<T>(sequence.GetEnumerator());
    }

    public struct WithIndexIterator<T> : IEnumerator<(T, int)>, IEnumerable<(T, int)>
    {
        private readonly IEnumerator<T> _en;
        private int _index;

        public WithIndexIterator(IEnumerator<T> en)
        {
            _en = en;
            _index = 0;
        }

        public (T, int) Current => (_en.Current, _index);

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            _en.Dispose();
        }

        public WithIndexIterator<T> GetEnumerator()
        {
            return this;
        }

        IEnumerator<(T, int)> IEnumerable<(T, int)>.GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            if (_en.MoveNext())
            {
                _index += 1;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _en.Reset();
            _index = 0;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }
    }

    public static IncrementalValuesProvider<T> NotNull<T>(this IncrementalValuesProvider<T?> collection)
        where T : struct
    {
        return collection
            .Where(static (m) => m.HasValue)
            .Select(static (m, _) => m!.Value);
    }

    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> sequence)
        where T : struct
    {
        foreach (var item in sequence)
        {
            if (item.HasValue)
            {
                yield return item.Value;
            }
        }
    }

    public static AttributeSyntax? FindMemAttr<M>(this SynModel<M> member, Func<SynModel<M>, AttributeSyntax, bool> predicate)
        where M : MemberDeclarationSyntax
    {
        foreach (var attrListSyntax in member.Node.AttributeLists)
        {
            foreach (var attrSyntax in attrListSyntax.Attributes)
            {
                if (predicate(member, attrSyntax))
                {
                    return attrSyntax;
                }
            }
        }

        return null;
    }

    public static AttributeSyntax? FindTypeAttr<T>(this SynModel<T> type, Func<SynModel<T>, AttributeSyntax, bool> predicate)
        where T : BaseTypeDeclarationSyntax
    {
        foreach (var attrListSyntax in type.Node.AttributeLists)
        {
            foreach (var attrSyntax in attrListSyntax.Attributes)
            {
                if (predicate(type, attrSyntax))
                {
                    return attrSyntax;
                }
            }
        }

        return null;
    }

    public static IEnumerable<AttributeSyntax> FilterMemAttr<M>(this SynModel<M> member, Func<SynModel<M>, AttributeSyntax, bool> predicate)
        where M : MemberDeclarationSyntax
    {
        foreach (var attrListSyntax in member.Node.AttributeLists)
        {
            foreach (var attrSyntax in attrListSyntax.Attributes)
            {
                if (predicate(member, attrSyntax))
                {
                    yield return attrSyntax;
                }
            }
        }
    }

    public static IEnumerable<AttributeSyntax> FilterTypeAttr<T>(this SynModel<T> type, Func<SynModel<T>, AttributeSyntax, bool> predicate)
        where T : BaseTypeDeclarationSyntax
    {
        foreach (var attrListSyntax in type.Node.AttributeLists)
        {
            foreach (var attrSyntax in attrListSyntax.Attributes)
            {
                if (predicate(type, attrSyntax))
                {
                    yield return attrSyntax;
                }
            }
        }
    }

    public static (NamespaceDeclarationSyntax, ImmutableArray<P>) GetHierarchy<P>(this CSharpSyntaxNode node)
        where P : MemberDeclarationSyntax
    {
        var nesting = ImmutableArray.CreateBuilder<P>(16);
        SyntaxNode? p = node;
        while ((p = p?.Parent) is not null)
        {
            switch (p)
            {
                case P member:
                    nesting.Add(member);
                    break;
                case NamespaceDeclarationSyntax ns:
                    return (ns, nesting.ToImmutable());
                default:
                    throw new InvalidOperationException($"{p.GetType().Name} is not allowed in the hierarchy.");
            }
        }

        throw new InvalidOperationException("No namespace declaration found.");
    }

    public static string? GetTypeName(this SemanticModel model, SyntaxNode node)
    {
        // Are we a type?
        var typeInfo = model.GetTypeInfo(node);
        if (typeInfo.Type is not null)
        {
            return typeInfo.Type.ToDisplayString();
        }

        var decl = model.GetDeclaredSymbol(node);
        // Are we of a type?
        if (decl?.ContainingType is not null)
        {
            return decl.ContainingType.ToDisplayString();
        }
        // Do we have any symbol at all?
        return decl?.ToDisplayString();
    }

    public static IEnumerable<T> CollectSyntax<T>(this Compilation comp, Func<SyntaxNode, CancellationToken, bool> predicate, Func<Compilation, SyntaxNode, CancellationToken, T> transform)
    {
        foreach (var tree in comp.SyntaxTrees)
        {
            CancellationToken ct = new();
            if (tree.TryGetRoot(out var root))
            {
                Stack<SyntaxNode> stack = new(64);
                stack.Push(root);

                SyntaxNode node;
                while ((node = stack.Pop()) is not null)
                {
                    foreach (var child in node.ChildNodesAndTokens())
                    {
                        if (child.IsNode)
                        {
                            stack.Push((SyntaxNode)child!);
                        }
                    }

                    if (predicate(node, ct))
                    {
                        yield return transform(comp, node, ct);
                    }
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
    }
}
