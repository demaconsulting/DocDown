using System.Reflection;
using DocDown.Core;
using DocDown.PowerPoint.Rendering;

namespace DemaConsulting.DocDown.PowerPoint.Rendering.Tests;

/// <summary>
///     Unit tests for <see cref="PowerPointRenderingDocDownBuilderExtensions"/>: that it registers
///     the rendering extractor, returns the builder for chaining, rejects a null builder, and
///     exposes no CanvasNet rasterizer type on its public surface.
/// </summary>
/// <remarks>
///     The registration seam is the one visible edge from a host to this assembly and,
///     transitively, to the rasterization stack. These tests hold that edge to the same standard as
///     the managed PDF rendering package's seam: explicit, chainable, null-rejecting, and free of
///     any DemaConsulting.CanvasNet/DemaConsulting.CanvasNet.Pptx/DemaConsulting.CanvasNet.Charts
///     type on the surface a host compiles against.
/// </remarks>
public class PowerPointRenderingDocDownBuilderExtensionsTests
{
    /// <summary>
    ///     Proves the extension registers exactly the rendering extractor.
    /// </summary>
    [Fact]
    public void AddPowerPointRendering_OnBuilder_RegistersRenderingExtractor()
    {
        // Arrange & Act: register through the seam and build
        var engine = new DocDownBuilder().AddPowerPointRendering().Build();

        // Assert: exactly the rendering extractor is registered
        Assert.Single(engine.Extractors);
        Assert.Equal("powerpoint-rendering", engine.Extractors[0].Id);
    }

    /// <summary>
    ///     Proves the extension returns the same builder so registration can be chained.
    /// </summary>
    [Fact]
    public void AddPowerPointRendering_OnBuilder_ReturnsSameBuilderForChaining()
    {
        // Arrange
        var builder = new DocDownBuilder();

        // Act
        var returned = builder.AddPowerPointRendering();

        // Assert
        Assert.Same(builder, returned);
    }

    /// <summary>
    ///     Proves the extension rejects a null builder at the point of the call.
    /// </summary>
    [Fact]
    public void AddPowerPointRendering_NullBuilder_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ((DocDownBuilder)null!).AddPowerPointRendering());
    }

    /// <summary>
    ///     Proves no DemaConsulting.CanvasNet type (CanvasNet, CanvasNet.Pptx, or CanvasNet.Charts)
    ///     appears on this package's public surface.
    /// </summary>
    /// <remarks>
    ///     Scans the public members of both public types for any signature type whose assembly is
    ///     the rasterization stack. A leak there would drag the rasterizer's types into a host's
    ///     compilation, which the separate-package design exists to prevent.
    /// </remarks>
    [Fact]
    public void PublicApi_AllPublicMembers_ExposeNoCanvasNetTypes()
    {
        // Arrange: the forbidden assembly names on the public surface
        var forbidden = new[] { "DemaConsulting.CanvasNet" };
        var publicTypes = typeof(PowerPointRenderingDocDownBuilderExtensions).Assembly
            .GetExportedTypes();

        // Act: collect every signature type of every public member of every public type
        var leaks = new List<string>();
        foreach (var type in publicTypes)
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var signatureType in SignatureTypes(member))
                {
                    var assemblyName = signatureType.Assembly.GetName().Name ?? string.Empty;
                    if (forbidden.Any(name => assemblyName.StartsWith(name, StringComparison.Ordinal)))
                    {
                        leaks.Add($"{type.Name}.{member.Name} -> {signatureType.FullName}");
                    }
                }
            }
        }

        // Assert: the public surface mentions no rasterizer type at all
        Assert.True(leaks.Count == 0, "CanvasNet rasterizer types reached the public API surface:\n  " + string.Join("\n  ", leaks));
    }

    /// <summary>
    ///     Enumerates every type that appears in a member's signature.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The parameter, return, property, and field types involved.</returns>
    /// <remarks>Mirrors the managed PDF rendering package's public-API leak test so both packages hold one standard. Pure.</remarks>
    private static IEnumerable<Type> SignatureTypes(MemberInfo member)
    {
        var types = new List<Type>();
        switch (member)
        {
            case MethodBase method:
                types.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                if (method is MethodInfo info)
                {
                    types.Add(info.ReturnType);
                }

                break;
            case PropertyInfo property:
                types.Add(property.PropertyType);
                break;
            case FieldInfo field:
                types.Add(field.FieldType);
                break;
            default:
                break;
        }

        return types;
    }
}
