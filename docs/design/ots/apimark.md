## ApiMark

### Purpose

`DemaConsulting.ApiMark.MSBuild` generates a compact, AI-friendly Markdown API reference from a
compiled assembly plus its XML documentation comments, and places that reference inside the
produced NuGet package. It was chosen because its output is structured for *gradual disclosure*:
an `api.md` index lists the namespaces, each namespace page lists its types, each type page
carries the signature, the XML `<summary>` and remarks prose, and member tables that link on to
one page per member. A reader — human or model — descends only as far as the task requires
instead of loading a single monolithic reference.

That is the same principle DocDown itself is built on. DocDown exists to turn documents into a
predictable layout a multimodal agent can consume progressively; ApiMark applies the identical
idea to DocDown's own public API. Shipping the reference inside the package means a consumer who
has restored `DemaConsulting.DocDown.Core` already has the API documentation locally, with no
network access and no documentation site to resolve.

ApiMark is MIT-licensed, compatible with this repository's MIT license, and carries no package
dependencies of its own: the `.nupkg` contains only the MSBuild task assembly, the `apimark`
tool, and the tool's private closure (Mono.Cecil, ANTLR runtime, and
`Microsoft.Extensions.FileSystemGlobbing`), none of which enters this repository's dependency
graph.

### Prerelease Version — a Recorded Judgement Call

**The pinned version, `0.5.0-beta.3`, is a prerelease.** Stable ApiMark releases exist (the
`0.4.x` line, most recently `0.4.10`), so pinning a beta in a repository with this compliance
posture is a deliberate judgement rather than the absence of an alternative. It is recorded here
so that it is a decision on the record rather than an unnoticed default.

The reasoning, and the exposure it accepts:

- **What is exposed is documentation, not runtime behavior.** ApiMark is a build-time tool with
  `PrivateAssets="All"`. It contributes no assembly, no native asset, and no dependency to any
  shipped package. A defect in ApiMark can produce wrong, ugly, or missing Markdown in the
  packaged `api/` folder; it cannot change what `DocDown.Core` does at run time, cannot alter the
  compiled output, and cannot reach a consumer's application code.
- **The failure mode is loud, not silent.** The MSBuild task runs after `Build` and fails the
  build on error. A regression surfaces as a red build in CI, not as a quietly corrupt artifact.
- **The prerelease is what supports the feature being used.** `ApiMarkPackDocs` is wired through
  `TargetsForTfmSpecificContentInPackage`, and the `0.5.0` line is where that packaging path and
  the `--format` selection it depends on are current.
- **The pin is exact.** `[0.5.0-beta.3]` cannot float onto `0.5.0-beta.4` or a later beta, so no
  restore can change the generator without a reviewed commit.

The exposure is therefore bounded to the content of a documentation folder inside the package,
and is accepted on that basis. The pin should be moved to a stable `0.5.x` release once one is
published.

### Features Used

- **MSBuild integration for C# projects.** The `GenerateApiMarkDocumentation` target runs
  `AfterTargets="Build"` and invokes the `ApiMarkTask` against `$(TargetPath)` and
  `$(DocumentationFile)`. `GenerateDocumentationFile` is already `true` in every project in this
  repository, which is the tool's only prerequisite, so no project needed changing to satisfy it.
- **`ApiMarkOutputDir`** — left at its default, `$(MSBuildProjectDirectory)\api`, so each
  packable library writes `src/DemaConsulting.DocDown.<Name>/api/`.
- **`ApiMarkPackDocs=true`** — adds `$(ApiMarkOutputDir)\**\*` to the pack inputs as
  `TfmSpecificPackageFile` items with `PackagePath="api/..."`, placing the reference in the
  `api/` folder of the `.nupkg`. This is the reason the item is here at all.
- **`ApiMarkVisibility`** — left at its default, `Public`. Documenting protected members was
  considered and rejected: DocDown's extension points are interfaces and sealed classes, and
  `PublicAndProtected` would add no consumer-relevant surface.
- **`DisableApiMark=true`** — used to switch generation off for the projects that must not
  produce a reference (see below).
- **`apimark --validate --results <file>.trx`** — the tool's built-in self-validation suite, run
  in CI to produce the OTS verification evidence.

The C++ and VHDL front ends, `--enforce-docs` documentation-coverage enforcement, the
`--format single-file` output mode, and the standalone CLI generation path are **not** used and
are outside the scope of this integration.

### Integration Pattern

**Build-time only, never a shipped dependency.** The package is referenced with
`PrivateAssets="All"` in each of the seven packable libraries — `Core`, `Pdf`, `Pdf.Rendering`,
`Word`, `Visio`, `PowerPoint`, and `Excel`. It contributes MSBuild targets and nothing else, so
it does not appear in any produced `.nuspec` dependency list and does not flow to consumers. What
*does* flow to consumers is the generated Markdown, as package content rather than as a
dependency.

**Version pinning — the same rule applied to PdfPig and the Open XML SDK.** The reference is
pinned to the exact range `[0.5.0-beta.3]` for restore determinism and SBOM reproducibility: a
floating reference would allow a restore to substitute a different generator and silently change
the documentation shipped in a package. The pin is also what bounds the prerelease exposure
described above.

**Disabled where there is no consumer API.** Every test project and `DemaConsulting.DocDown.TestSupport`
set `DisableApiMark=true`. They are not packable and expose no consumer surface, so a generated
reference would be an unused build artifact. ApiMark's targets do not currently reach these
projects — the libraries' `PrivateAssets="All"` stops build assets flowing across a
`ProjectReference` — so the setting is declarative today; it is stated explicitly so the intent
survives any later move of the reference into a shared props file.

**Disabled for `DocDown.Tool`, on measured evidence.** The tool is packable, so it was a genuine
decision rather than a default. Every type in `DemaConsulting.DocDown.Tool` is `internal`, and
running the generator directly against the Release build —
`apimark dotnet --assembly DemaConsulting.DocDown.Tool.dll --xml-doc DemaConsulting.DocDown.Tool.xml
--output <dir>` — reports `Found 0 types across 0 namespace(s)` and emits a single `api.md`
holding an empty namespace table and the path-convention boilerplate. Packing that would ship a
hollow artifact that implies an API surface the tool does not offer, so generation is disabled
rather than enabled. Widening `ApiMarkVisibility` to expose the internals was rejected on the
same grounds: internal types are not a consumer contract. The tool's user-facing surface is its
command line, which is documented in the README and the User Guide.

**Generated output is build output.** The `api/` folder is regenerated from the assembly and its
XML doc comments on every build and is never committed. It is excluded in three places, and for
one reason — it is build output, not a relaxation of any authored-content rule:

- `.gitignore` (`src/*/api/`) so it cannot be committed;
- `.markdownlint-cli2.yaml` `ignores` (`**/api/**`), because generated signature lines and member
  tables are not authored documentation and are not subject to the prose formatting rules;
- `.cspell.yaml` `ignorePaths` (`**/api/**`), because generated type and member identifiers are
  not prose and no spelling judgement applies to them.

Omitting any one of the three produces a lint gate whose result depends on whether a build has
recently run — the precise failure mode this repository has already hit twice with `TestResults/`.

**Multi-targeting.** Each library targets `net8.0;net9.0;net10.0`. ApiMark's target runs for the
first framework in `TargetFrameworks` only (`net8.0`), and the pack hook contributes the same
single `api/` folder regardless of framework, so a multi-targeted package carries exactly one
copy of the reference rather than one per framework.

**Concurrent packs — a measured consequence, not an anticipated one.** Writing generated output
into the project directory makes `api/` a *shared* path, and this repository's packaging tests
run `dotnet pack` on the same real project from several processes at once. Adding ApiMark made
five of them fail with `The process cannot access the file '…\api\api.md' because it is being
used by another process` — the same class of cross-process collision the suite had already
solved for the generated `obj/Release/<id>.nuspec`. It was found by running the tests, not by
reading the targets.

The fix follows the existing precedent. Each packable library accepts an optional
`ApiMarkOutputRoot` property and composes `$(ApiMarkOutputRoot)\$(MSBuildProjectName)\api` from
it; when the property is unset — which is every ordinary build and every CI build — ApiMark's
own `$(MSBuildProjectDirectory)\api` default applies unchanged. `NuGetPackHelper` passes
`-p:ApiMarkOutputRoot=<unique folder>` per invocation. The per-project subfolder is
load-bearing: an MSBuild global property flows into referenced projects, and ApiMark *merges*
into an existing output directory rather than clearing it, so a single shared folder would have
put `DocDown.Core`'s type pages inside `DocDown.Pdf`'s package. The CI pipeline is unaffected —
it builds once and packs with `--no-build`, so generation runs exactly once per project and
nothing races.
