namespace DocDown.Core;

/// <summary>
///     Controls how the scratch output folder is prepared before an extraction writes into it.
/// </summary>
/// <remarks>
///     The mode is a safety and hygiene control: it decides whether Core may delete existing
///     content, must refuse a populated folder, or should pick a fresh unique folder — so an
///     extraction never clobbers unrelated files by accident.
/// </remarks>
public enum ScratchFolderMode
{
    /// <summary>
    ///     Refuse to proceed if the target folder exists and is not empty.
    /// </summary>
    RequireEmpty,

    /// <summary>
    ///     Delete only the files a <c>manifest.json</c> written for this very folder accounts for,
    ///     refusing the whole operation when the folder holds anything that manifest does not list;
    ///     use <see cref="Overwrite"/> to replace a folder's contents deliberately.
    /// </summary>
    CleanIfDocDownFolder,

    /// <summary>
    ///     Delete any existing contents unconditionally before writing.
    /// </summary>
    Overwrite,

    /// <summary>
    ///     Append a numeric suffix until an unused folder name is found, leaving existing folders
    ///     untouched.
    /// </summary>
    CreateUnique
}
