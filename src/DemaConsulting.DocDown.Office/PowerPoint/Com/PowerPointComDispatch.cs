using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DocDown.PowerPoint.OpenXml;

namespace DocDown.PowerPoint.Com;

/// <summary>
///     The mechanical late-bound COM plumbing the PowerPoint automation adapter is built on:
///     IDispatch property and method access, single-instance activation with owned-process capture,
///     a watchdog that runs a session on a dedicated single-threaded-apartment thread and
///     force-terminates the owned PowerPoint process if it hangs, and deterministic release of COM
///     objects.
/// </summary>
/// <remarks>
///     This unit carries no rendering policy — it only reflects calls onto a live PowerPoint object
///     and tears the session down. It is factored out of <see cref="PowerPointAutomation"/> so the
///     adapter stays small. It is part of the untestable COM boundary and is exercised only by the
///     release-time self-test cases. Windows-only.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class PowerPointComDispatch
{
    /// <summary>The name (without extension) of the PowerPoint host process this adapter owns and may terminate.</summary>
    private const string PowerPointProcessName = "POWERPNT";

    /// <summary>Serializes activation so the before/after process diff that identifies the owned process is atomic.</summary>
    private static readonly object ActivationLock = new();

    /// <summary>Reads a property from a COM object by name through IDispatch.</summary>
    /// <param name="target">The COM object.</param>
    /// <param name="name">The property name.</param>
    /// <param name="args">Any index arguments the property takes.</param>
    /// <returns>The property value, or <see langword="null"/>.</returns>
    public static object? Get(object target, string name, params object[] args) =>
        target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, args, CultureInfo.InvariantCulture);

    /// <summary>Sets a property on a COM object by name through IDispatch.</summary>
    /// <param name="target">The COM object.</param>
    /// <param name="name">The property name.</param>
    /// <param name="value">The value to set.</param>
    public static void Set(object target, string name, object value) =>
        target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, [value], CultureInfo.InvariantCulture);

    /// <summary>Invokes a method on a COM object by name through IDispatch.</summary>
    /// <param name="target">The COM object.</param>
    /// <param name="name">The method name.</param>
    /// <param name="args">The positional arguments.</param>
    /// <returns>The method result, or <see langword="null"/>.</returns>
    public static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args, CultureInfo.InvariantCulture);

    /// <summary>Reads the integer <c>Count</c> property of a COM collection.</summary>
    /// <param name="collection">The COM collection.</param>
    /// <returns>The count.</returns>
    public static int Count(object collection) => AsInt(Get(collection, "Count"));

    /// <summary>Reads an indexed item from a COM collection by its 1-based position.</summary>
    /// <param name="collection">The COM collection.</param>
    /// <param name="index">The 1-based index.</param>
    /// <returns>The item.</returns>
    public static object Item(object collection, int index) =>
        Invoke(collection, "Item", index) ?? throw new PowerPointExtractionException(
            "Microsoft PowerPoint returned no item for the requested collection index.");

    /// <summary>Converts a COM-returned value to an <see langword="int"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer, or zero when null.</returns>
    public static int AsInt(object? value) => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    /// <summary>Converts a COM-returned value to a <see langword="double"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The double, or zero when null.</returns>
    public static double AsDouble(object? value) => value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    /// <summary>Finalizes and releases a runtime callable wrapper if the argument is a live COM object.</summary>
    /// <param name="comObject">The object to release, which may be null or non-COM.</param>
    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            try
            {
                Marshal.FinalReleaseComObject(comObject);
            }
#pragma warning disable CA1031 // Releasing a dead RCW after a forced kill must never mask the original outcome
            catch (Exception)
#pragma warning restore CA1031
            {
                // The owning process may already have been terminated by the watchdog
            }
        }
    }

    /// <summary>Forces the garbage collector to release any transient runtime callable wrappers.</summary>
    /// <remarks>Run before quitting so no lingering wrapper keeps the PowerPoint process alive.</remarks>
    public static void CollectComObjects()
    {
        // Releasing COM runtime callable wrappers requires an explicit collect-and-finalize cycle.
        // Without it a lingering wrapper keeps the host process alive and orphaned. Run the cycle
        // twice so finalizers that themselves release wrappers are collected in turn.
#pragma warning disable S1215 // An explicit collect is the documented mechanism for releasing COM RCWs deterministically
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();
#pragma warning restore S1215
    }

    /// <summary>
    ///     Activates a new single-use PowerPoint application and identifies the process it started.
    /// </summary>
    /// <returns>The PowerPoint application object and the process id it owns, or zero when it could not be isolated.</returns>
    /// <exception cref="PowerPointExtractionException">Thrown when PowerPoint is not registered or cannot be activated.</exception>
    /// <remarks>
    ///     The owned process id is captured by diffing the set of PowerPoint host processes across
    ///     activation under a lock, so the watchdog can terminate exactly the process this adapter
    ///     started and no other.
    /// </remarks>
    public static (object Application, int OwnedProcessId) Activate()
    {
        lock (ActivationLock)
        {
            var before = PowerPointProcessIds();
            var progId = Type.GetTypeFromProgID("PowerPoint.Application", throwOnError: false)
                ?? throw new PowerPointExtractionException(
                    "Microsoft PowerPoint is not registered on this machine, so the COM automation backend cannot "
                    + "render the deck.");
            var application = Activator.CreateInstance(progId)
                ?? throw new PowerPointExtractionException(
                    "Microsoft PowerPoint COM automation could not be activated on this machine.");

            var after = PowerPointProcessIds();
            after.ExceptWith(before);
            return (application, after.Count == 1 ? after.First() : 0);
        }
    }

    /// <summary>
    ///     Runs a PowerPoint session on a dedicated single-threaded-apartment thread under a timeout,
    ///     and terminates the owned process if the session hangs.
    /// </summary>
    /// <param name="session">The session to run; it must set the owned process id into <paramref name="ownedProcessId"/> as soon as it activates PowerPoint.</param>
    /// <param name="ownedProcessId">A single-element holder the session writes the owned process id into.</param>
    /// <param name="timeout">The maximum time the session may take before it is treated as a hang.</param>
    /// <exception cref="PowerPointExtractionException">Thrown when the session times out; the owned process is terminated first.</exception>
    /// <remarks>
    ///     A modal dialog blocks a PowerPoint automation call indefinitely even with alerts disabled,
    ///     so this watchdog is the universal backstop: on timeout it kills the owned process, which
    ///     unblocks and faults the worker, and reports an honest failure. Because every COM object is
    ///     created and released on the worker thread within the timeout window, nothing outlives the
    ///     watchdog and no orphaned process remains.
    /// </remarks>
    public static void RunSession(Action session, int[] ownedProcessId, TimeSpan timeout)
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                session();
            }
#pragma warning disable CA1031 // The watchdog records every worker fault as data so it can be rethrown on the caller's thread
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "DocDown PowerPoint COM session"
        };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();

        if (!worker.Join(timeout))
        {
            KillProcess(ownedProcessId[0]);
            throw new PowerPointExtractionException(
                $"Microsoft PowerPoint did not respond within {(int)timeout.TotalSeconds} seconds while rendering "
                + "the deck, most likely because it raised a modal dialog. The owned PowerPoint process was terminated.");
        }

        if (failure is not null)
        {
            throw failure is TargetInvocationException { InnerException: { } inner }
                ? Rethrow(inner)
                : Rethrow(failure);
        }
    }

    /// <summary>Wraps a worker fault as a <see cref="PowerPointExtractionException"/> unless it already is one.</summary>
    /// <param name="exception">The worker fault.</param>
    /// <returns>An exception to rethrow on the caller's thread.</returns>
    private static Exception Rethrow(Exception exception) =>
        exception as PowerPointExtractionException
        ?? new PowerPointExtractionException(
            $"Microsoft PowerPoint COM automation failed while rendering the deck: {exception.Message}", exception);

    /// <summary>Terminates the owned PowerPoint process and waits briefly for it to exit.</summary>
    /// <param name="processId">The owned process id, or zero when none is owned.</param>
    private static void KillProcess(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
#pragma warning disable CA1031 // The process may already have exited; a kill failure must not mask the timeout
        catch (Exception)
#pragma warning restore CA1031
        {
            // Already gone or inaccessible
        }
    }

    /// <summary>Reads the set of PowerPoint host process ids currently running.</summary>
    /// <returns>The process ids.</returns>
    private static HashSet<int> PowerPointProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(PowerPointProcessName))
        {
            ids.Add(process.Id);
            process.Dispose();
        }

        return ids;
    }
}
