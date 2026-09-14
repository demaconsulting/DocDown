namespace DocDown.Office.Com;

/// <summary>
///     Helpers shared by the COM automation backends.
/// </summary>
/// <remarks>
///     A COM backend delegates text extraction to its managed Open XML counterpart and answers page
///     rendering itself. The two types here are what make that composition honest, and they are
///     shared because PowerPoint and Visio need exactly the same behavior from them.
/// </remarks>
internal static class NamespaceDoc
{
}
