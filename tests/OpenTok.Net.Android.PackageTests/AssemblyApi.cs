using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace OpenTok.Net.Android.PackageTests;

/// <summary>
/// Reads the public API out of a binding assembly using metadata only. The assembly targets
/// *-android and references Mono.Android, so it cannot be loaded into the test process; the
/// metadata reader lets these tests run on a plain desktop runner with no emulator.
/// </summary>
public sealed class AssemblyApi : IDisposable
{
    private readonly PEReader _peReader;
    private readonly MetadataReader _metadata;
    private IReadOnlyList<string>? _publicTypes;

    public AssemblyApi(Stream assembly)
    {
        _peReader = new PEReader(assembly);
        _metadata = _peReader.GetMetadataReader();
    }

    /// <summary>
    /// Namespace-qualified names of every public top-level type. Nested types are excluded — the
    /// visibility filter below matches <see cref="TypeAttributes.Public"/> exactly, and a nested
    /// type carries <see cref="TypeAttributes.NestedPublic"/> instead.
    /// </summary>
    public IReadOnlyList<string> PublicTypes => _publicTypes ??= _metadata.TypeDefinitions
        .Select(_metadata.GetTypeDefinition)
        .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public)
        .Select(FullNameOf)
        .ToList();

    public IReadOnlyList<string> MethodsOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetMethods()
            .Select(_metadata.GetMethodDefinition)
            .Where(method => (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public)
            .Select(method => _metadata.GetString(method.Name))
            .ToList();
    }

    public IReadOnlyList<string> PropertiesOf(string typeFullName)
    {
        var type = FindType(typeFullName);
        return type.GetProperties()
            .Select(_metadata.GetPropertyDefinition)
            .Select(property => _metadata.GetString(property.Name))
            .ToList();
    }

    private TypeDefinition FindType(string typeFullName)
    {
        foreach (var handle in _metadata.TypeDefinitions)
        {
            var type = _metadata.GetTypeDefinition(handle);
            if (FullNameOf(type) == typeFullName)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Type '{typeFullName}' is not defined in this assembly.");
    }

    /// <summary>
    /// The name callers use to ask for a type: <c>Namespace.Type</c>, and
    /// <c>Namespace.Outer/Nested</c> for a nested one.
    /// </summary>
    /// <remarks>
    /// A nested type's own <c>Namespace</c> row is empty in metadata — the namespace lives on the
    /// enclosing type — so the name has to be assembled by walking outwards. Without that, every
    /// nested type would come back as its bare name (<c>StreamReceivedEventArgs</c>), and the
    /// generated listener event-args types, of which there are dozens sharing names across
    /// <c>Session</c>, <c>PublisherKit</c> and <c>SubscriberKit</c>, would be indistinguishable.
    /// <c>/</c> rather than <c>+</c> or <c>.</c>: it is the one separator that cannot occur in an
    /// identifier, so the split is unambiguous, and it matches how ECMA-335 itself writes nesting.
    /// </remarks>
    private string FullNameOf(TypeDefinition type)
    {
        var name = _metadata.GetString(type.Name);

        if (type.IsNested)
        {
            return $"{FullNameOf(_metadata.GetTypeDefinition(type.GetDeclaringType()))}/{name}";
        }

        var ns = _metadata.GetString(type.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    public void Dispose() => _peReader.Dispose();
}
