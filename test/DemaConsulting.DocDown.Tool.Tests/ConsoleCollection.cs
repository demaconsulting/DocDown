namespace DemaConsulting.DocDown.Tool.Tests;

/// <summary>
///     Defines the <c>Console</c> test collection, which runs the test classes that replace the process-wide
///     <see cref="Console.Out"/> one at a time.
/// </summary>
/// <remarks>
///     <see cref="Console.SetOut"/> affects every test that runs at the same time. A class that captures the
///     console output carries <c>[Collection("Console")]</c>, so that no other such class replaces the writer
///     while it captures, and the output of <c>Program.Main</c> reaches the writer that the test reads.
/// </remarks>
[CollectionDefinition("Console")]
public sealed class ConsoleCollection;
