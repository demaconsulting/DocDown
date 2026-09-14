using System.Reflection;

namespace DocDown.Extraction;

/// <summary>
///     Reads a backend's embedded self-test probe document.
/// </summary>
/// <remarks>
///     <para>
///         Each extraction backend ships one small document, authored in the application whose
///         format it reads, as an embedded resource. A self-test extracts that document and checks
///         what came back, which is what makes the check meaningful: it proves the backend can read
///         what the real application emits, in this deployment.
///     </para>
///     <para>
///         Backends previously synthesized a document at run time instead, using the writer side of
///         whichever library they read with. That only ever proved a library agreed with itself —
///         it could not detect a failure to read genuine application output — and it put hundreds of
///         lines of document-writing code inside libraries whose whole purpose is reading.
///     </para>
/// </remarks>
public static class SelfTestProbe
{
    /// <summary>
    ///     Loads an embedded probe document as bytes.
    /// </summary>
    /// <param name="assembly">The assembly carrying the resource. Must not be null.</param>
    /// <param name="resourceName">The fully qualified resource name. Must not be null or empty.</param>
    /// <returns>The document's bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="resourceName"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the assembly carries no resource of that name, which means the package was
    ///     built without its probe and the self-test cannot run.
    /// </exception>
    /// <remarks>Reads from the assembly's own manifest, so there is no file system access and no working-directory dependency.</remarks>
    public static byte[] Load(Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"The embedded self-test probe '{resourceName}' is missing from " +
                $"'{assembly.GetName().Name}'. Available resources: " +
                $"{string.Join(", ", assembly.GetManifestResourceNames())}.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
