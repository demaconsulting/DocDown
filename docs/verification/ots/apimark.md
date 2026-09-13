## ApiMark Verification

This document provides the verification evidence for the ApiMark OTS software item. Requirements
for this OTS item are defined in the ApiMark OTS Software Requirements document.

### Required Functionality

`DemaConsulting.ApiMark.MSBuild` reads a compiled DocDown assembly and its XML documentation file
and writes a gradual-disclosure Markdown API reference — an `api.md` index, a page per namespace,
a page per type, and a page per member — into the project's `api/` folder, which is then packed
into the NuGet package. ApiMark contributes no runtime asset to any shipped package; the only
thing it can affect is the content of that documentation folder.

### Verification Approach

ApiMark is verified from its **built-in self-validation suite**, the same evidence pattern used
for the other DEMA tools in this repository (ReviewMark, ReqStream, VersionMark, SysML2Tools).
The `.github/workflows/build.yaml` build job runs:

```text
dotnet apimark --validate --results artifacts/apimark-self-validation-<os>.trx
```

on each of the three build operating systems, immediately before the solution is built. The TRX
lands in `artifacts/`, which is the glob ReqStream consumes
(`--tests "artifacts/**/*.trx"`), so the result is ordinary traceability evidence rather than an
assertion made in prose.

**A dedicated OTS test project is deliberately not created.** ApiMark's own suite exercises its
generator against controlled inputs more directly than a DocDown test could, and re-testing the
generator from this repository would duplicate it without adding information.

Exactly one self-validation test is claimed as evidence: `ApiMark_DotNetGeneration`. The suite
also contains `ApiMark_VersionDisplay`, `ApiMark_HelpDisplay`, `ApiMark_CppGeneration`,
`ApiMark_VhdlGeneration`, `ApiMark_DotNetEnforceDocs`, `ApiMark_CppEnforceDocs`, and
`ApiMark_VhdlEnforceDocs`. None of those is claimed here, because this repository uses neither
the C++ front end, nor the VHDL front end, nor documentation-coverage enforcement, and does not
invoke the CLI's version or help paths. Coverage is stated as the one test that exercises the
path DocDown depends on, not as "the suite passes".

Note that `ApiMark_CppGeneration` and `ApiMark_CppEnforceDocs` report **Skipped** on runners
without `clang` installed. Neither is claimed as evidence, so this does not affect the
requirements coverage recorded below.

### Test Scenarios

#### ApiMark_DotNetGeneration

**Scenario**: ApiMark self-validation invokes the `dotnet` generation path against a controlled
assembly and its XML documentation file, writing Markdown to a scratch output directory.

**Expected**: Exits 0 and produces the gradual-disclosure output set — an `api.md` index listing
the discovered namespaces, a page per namespace, and a page per type with its signature, summary
prose, and member tables.

**Requirement coverage**: `DocDown-OTS-ApiMark-DotNetGeneration`.

### Evidence Limitations

Two things about this integration are **not** covered by an automated requirement, and are
recorded here rather than implied to be verified:

- **The `ApiMarkPackDocs` packaging hook.** ApiMark's self-validation exercises generation, not
  the `_ApiMarkIncludeDocsInPackage` pack target. Placement of the `api/` folder inside the
  produced `.nupkg` was confirmed by inspecting real packages — `dotnet pack` on
  `DemaConsulting.DocDown.Core` and `DemaConsulting.DocDown.Word`, then opening the resulting
  `.nupkg` archives and enumerating their `api/` entries — rather than by reading the project
  files. No requirement claims automated coverage of it.
- **The content of the generated reference.** Whether a given type page carries the prose an
  author intended is a property of the XML doc comments in the source, not of ApiMark, and is not
  asserted anywhere.

Neither gap can affect shipped runtime behavior: ApiMark is referenced with `PrivateAssets="All"`
and contributes only Markdown content to a package.

### Requirements Coverage

- **`DocDown-OTS-ApiMark-DotNetGeneration`**: ApiMark_DotNetGeneration
