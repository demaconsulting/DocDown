### ScratchFolder Verification Design

This document describes the unit-level verification strategy for `ScratchFolder`.

#### Verification Approach

`ScratchFolder` is verified in `ScratchFolderTests.cs` through adversarial unit tests. The tests use a
real filesystem through `TempScratch` because the unit's value lies in what it accepts, refuses, and
deletes on disk.

#### Test Environment

- **Framework**: xUnit v3 under the .NET SDK
- **Targets**: net8.0, net9.0, and net10.0
- **Filesystem**: real per-test `TempScratch` folders
- **Inputs**: hostile path strings and hand-authored manifest fixtures

#### Acceptance Criteria

Per IEC 62304 §5.5.2, `ScratchFolder` passes when traversal and rooted paths are refused, safe
relative paths remain contained, illegal characters and reserved device names are rejected, overlong
paths fail with `ScratchFolderException`, slugging is deterministic and Windows-portable,
`CleanIfDocDownFolder` deletes only proven prior DocDown output for the same folder, folder changes
during preparation are refused, and `Overwrite` clears contents unconditionally.

#### Test Scenarios

##### Traversal and rooted paths are refused

**Test**: `ScratchFolder_Combine_TraversalOrRootedPath_IsRefused`

##### A legitimate relative path stays contained

**Test**: `ScratchFolder_Combine_LegitimateRelativePath_StaysContained`

##### Illegal characters are refused

**Test**: `ScratchFolder_Combine_InjectionCharacterInComponent_IsRefused`

##### Reserved device names are detected and refused

**Tests**: `ScratchFolder_IsReservedDeviceName_ReservedComponent_ReturnsTrue`,
`ScratchFolder_IsReservedDeviceName_OrdinaryComponent_ReturnsFalse`,
`ScratchFolder_Combine_ReservedDeviceNameComponent_IsRefused`,
`ScratchFolder_IsReservedDeviceName_OrdinalPrefixedName_IsNotReserved`

##### Overlong paths fail as ScratchFolderException

**Tests**: `ScratchFolder_ValidateTotalPathLength_OverLimit_ThrowsScratchFolderException`,
`ScratchFolder_Combine_OverLongTotalPath_ThrowsScratchFolderException`

##### Slugging remains deterministic and Windows-portable

**Tests**: `ScratchFolder_Slugify_TrailingDotsAndSpaces_AreRemoved`,
`ScratchFolder_Slugify_AccentedAndPunctuated_ProducesCleanAsciiSlug`

##### CleanIfDocDownFolder refuses user folders and cleans proven DocDown output

**Tests**: `ScratchFolder_Prepare_CleanIfDocDownOnUserFolder_RefusesAndPreservesContents`,
`ScratchFolder_Prepare_CleanIfDocDownOnDocDownFolder_CleansInventoriedFiles`

##### Copied or mismatched manifests are refused

**Test**: `ScratchFolder_Prepare_CopiedGenuineManifestAmongUserFiles_RefusesAndPreservesContents`

##### Unaccounted files and escaping manifest paths are refused

**Tests**: `ScratchFolder_Prepare_UnaccountedFileBesideManifest_RefusesAndPreservesContents`,
`ScratchFolder_Prepare_EscapingManifestPath_Refuses`

##### Folder changes during preparation are refused

**Test**: `ScratchFolder_Prepare_FolderChangesDuringPreparation_Refuses`

##### Overwrite clears any existing contents

**Test**: `ScratchFolder_Prepare_OverwriteOnPopulatedFolder_ClearsContents`
