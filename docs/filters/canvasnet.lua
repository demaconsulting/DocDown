--[[
canvasnet.lua - a Pandoc filter that draws models, charts, diagrams, and document pages as PNG pictures.

A document refers to a source file like any picture, and says how to draw it in attributes:

    ![Pump mount](pump-mount.scad){view=iso width=60%}
    ![Sales by region](sales.vl.json){scale=2}
    ![The wiring](wiring.vsdx){page=2 dpi=150}
    ![Order flow](order-flow.mmd){theme=forest}

The filter handles a picture whose file name ends in one of these (any other picture is left alone,
and the tool is not even started for it, so PNG and JPEG pictures, and SVG pictures that link none of these,
behave as they always did):

    .scad       an OpenSCAD model (the subset the tool draws)
    .vl.json    a Vega-Lite chart (.vl is the same)
    .vsdx       a page of a Visio drawing
    .pptx       a slide of a PowerPoint presentation
    .pdf        a page of a PDF document
    .mmd        a Mermaid diagram (.mermaid is the same)
    .svg        an SVG drawing whose <image> elements link one of the above, directly or through a nested .svg
                file: flattened with "canvasnet flatten", see below; any other SVG drawing is left alone

Attributes this filter reads (all optional; "all" means every input type above):

    size         all but .svg: the picture in pixels, WIDTHxHEIGHT; for a model the default is 1600x1200
                 (unless scale is given), the others are drawn at their natural size (dpi and scale apply)
    scale        all: multiply the natural size by a number, 0.001 to 8192, for example 2
                 (not together with size); for .svg, the size of the pictures that replace the links
    background   all but .svg: a color such as #F0F0F0 or #F0F0F0FF, a name such as white, or transparent
                 (default: white; the tool's own default for charts and documents)
    page         .vsdx, .pptx, .pdf: the page or slide, counted from 1 (default 1)
    dpi          .vsdx, .pptx, .pdf: the resolution of the page in dots per inch, above 0 up to 2400
                 (default 96; a whole number for .vsdx)
    view         .scad: iso, front, back, left, right, top or bottom (default: the model's own view, iso, or
                 top for a model that is entirely 2D)
    azimuth      .scad: degrees about the Z axis, 0 from the front
    elevation    .scad: degrees above the XY plane, -90 to 90
    projection   .scad: ortho (default) or perspective
    define-NAME  .scad: sets a model variable, for example define-bracket_height=60
    param-NAME   .vl.json, .vl: sets a parameter of the chart (--param NAME=VALUE: a number, true, false, null,
                 text, or JSON such as {"x":[2,6]} for a selection), for example param-cutoff=50
    theme        .mmd, .mermaid: default, base, dark, forest or neutral (as mmdc -t: a theme the diagram's own
                 configuration sets still wins)

An attribute for another input type (view on a chart, page on a model) is an error, not ignored.
Every other attribute (width, height, an id or a class) stays on the picture. An .svg picture reads only
scale; every other attribute of it stays on the picture.

SVG drawings. An SVG reader such as WeasyPrint draws nothing for an <image href="flow.mmd"> (or a .scad, .vl.json,
.pdf, .pptx, or .vsdx link, or a nested .svg file that links one). For an .svg picture the filter reads the drawing
and the .svg files it links (as the tool does: the href of every image element, or its xlink:href (an href in the
XLink namespace, under any prefix bound to it) when it has no href,
entities decoded, a query and a fragment cut off and then the rest percent-decoded, as the tool reads a link, remote and
data: links skipped, an absolute path and a file: URI without a host followed; comments, CDATA sections, processing
instructions and a DOCTYPE hold no links, and a ">" in a quoted attribute does not end a tag); when none of them links
such a file, the picture is left alone and the tool is not started, so a plain SVG drawing stays a vector picture
exactly as before. A link to such a file that does not exist counts too, so the tool reports it (svg-image-unresolved)
instead of the link being left silently broken. Otherwise the tool writes a
copy ("canvasnet flatten ... --json") in which each such link is a PNG picture of its placement size (times scale)
as a data: URI, and the picture points at the copy, docs/generated/canvasnet/<name>-<hash>.svg; the tool rewrites the
relative links that are left (rasters, plain .svg files) so they still find their files from there. When the tool
finds nothing to flatten (a diagram that the drawing never shows, or a missing file, which it reports as a warning),
the picture is left alone and a marker (<hash>.plain, holding the tool's warnings, which later builds show again) saves
later builds from starting the tool again.

A Mermaid diagram can also be written in the document, as a fenced code block of class mermaid:

    ```{.mermaid caption="Order flow" theme=forest width=80%}
    flowchart LR
        A[Order] --> B{Paid?} --> C[Ship]
    ```

The text is written to docs/generated/canvasnet/<hash>.mmd and drawn like a .mmd picture, and the block becomes
the picture: a figure with the caption when a caption attribute is given, else a paragraph holding the picture.
The block reads the same attributes as a .mmd picture (size, scale, background, theme); caption is taken
as the figure's caption, and every other attribute and any class but mermaid stay on the picture. The id
stays on the picture of a block without a caption; with a caption it goes on the figure, as Pandoc places the
id of a captioned image, so a cross-reference to the figure works the same way for both. A
picture in a node (A@{ img: "part.png" }, <img src="part.png">) is found from the folder the text is written to,
not from the document's folder, so a diagram that draws pictures must be a .mmd file of its own, used as
![Parts](parts.mmd); the tool's message about the missing picture is shown as a warning.

Diagram types the tool does not draw. The tool draws flowchart (and graph), sequenceDiagram, classDiagram,
stateDiagram (and stateDiagram-v2), erDiagram, pie, gantt, mindmap, packet (and packet-beta), and gitGraph diagrams
(a gantt chart without a today line: the tool sets no clock). For any other type (journey, timeline, and so on) the tool reports
mermaid-diagram-unsupported, and the filter leaves the code block (or the .mmd picture) exactly as written, so the
document shows the code, and warns with the tool's message ("...; it is left as written and not drawn"). No other
filter is expected to draw it: the CanvasNet build fails when a generated document still holds such a block or
picture. A filter given on the command line after "--defaults" still runs after this one (Pandoc runs the filters in
the order of its command line, a defaults file's filters taking the place of its --defaults option), so it sees only
the blocks left as written. A marker in the cache folder (<hash>.unsupported, holding the message) saves later
builds from starting the tool again for that diagram; the warning is shown on every build.
A flowchart that only embeds a picture of such a type (A@{ img: "pets.mmd" }) is drawn as usual; the tool's report
about the embedded picture, which names it in its origin, is shown as a warning.

How it works. The filter has two jobs only:

  1. Find the source file the way Pandoc finds a picture: an absolute name is used if the file opens; a
     relative name is joined to each folder of the document's resource path in turn and the first file that
     opens is taken. A "../" in the name is fine.
  2. Hash every file the tool will read, so that a change to any of them draws the picture again.

The filter is NOT a security boundary. It runs the author's own documents with the author's permissions,
and Pandoc itself embeds pictures from any path. What a model, a chart or a diagram may read is decided by the tool's
file-access mode when the picture is drawn: by default every regular local file, as Pandoc reads images; the opt-in
variables below (File access) confine it. The filter does no containment check of its own. A dependency the tool
cannot read still stops the build, with the tool's own message. The filter does not resolve symbolic
links. Paths are used and compared exactly as written (collapsing "." and ".."), so a file reached through
two spellings is simply hashed twice, which is harmless.

The picture is drawn by the canvasnet tool ("canvasnet render ... --json", or "canvasnet flatten ... --json"
for an SVG drawing), and the PNG (the SVG copy) is written to
docs/generated/canvasnet/ (git-ignored through **/generated/) under a name made from a SHA-1 of everything
that decides the picture:

  - the file, and every file it pulls in that exists: for a model every file named by include <...> or
    use <...> (followed recursively, relative to the including file, an absolute name as it is; at most 512
    files, counting the model); for a chart every local data file named by a "url" in a "data" object of the specification
    (relative to the specification's folder; a remote URL, which the tool never reads, is skipped; an absolute path
    or a file: URI without a host is hashed), and every
    local picture that an image mark names (the "url" of the mark, the literal "value" or "datum" of a "url" channel,
    and the links that the "url" field holds in the inline rows; a model with its includes, a chart with its own
    files, any other file as it is; a fragment such as "#page=2" or "#def.width=20" is dropped, and since the link is part of the
    specification, a changed fragment draws the chart again); for a diagram every local picture that a node
    names (the img of a shape's data and the src of an <img> tag in a label, relative to the diagram's folder,
    a fragment such as "#theme=dark" dropped; a model, chart or diagram with its own files, any other file as
    it is). The pictures of a chart that come from a data file or from a transform are not tracked; a chart
    that names more than 512 different data files or pictures, a diagram that names more than 512 pictures,
    or a model that with its includes is more than 512 files, stops the build, since a file left out of the
    hash could change without the picture being drawn again; for an SVG drawing every local file its image
    elements link (an .svg file with its own links, a model, chart or diagram with its own files, any other
    file as it is), and each missing model, chart, diagram or document it links as a "missing" entry, so creating
    the file later flattens the drawing again;
  - for an SVG drawing, its folder as seen from docs/generated/canvasnet/, since the links the tool rewrites in the
    copy lead from there to it;
  - the drawing attributes, with the defaults this filter adds;
  - the tool: its --version text, the CANVASNET_TOOL text, and, for a local build named by that text, a
    fingerprint of the DLLs beside it; the file-access arguments (see File access below); and this filter's
    version.

An unchanged source is therefore not drawn again, and a changed one always is. Each picture is written
under a temporary name and then renamed, so two builds running at once never read a half-written file.
Pandoc may run this filter more than once for one document (DocTemplates runs Pandoc first to work out
the file name); the second run finds the picture already drawn. The filter uses only pandoc.path,
pandoc.system, pandoc.json, pandoc.sha1, pandoc.pipe, pandoc.read and pandoc.utils (Pandoc 3.x), and
starts the tool with an argument list, never through a shell, so it runs the same on Windows and on a
Linux build server, and no attribute or file name can inject a command.

The tool. The command is "dotnet canvasnet", the tool a repository pins in its .config/dotnet-tools.json:

    dotnet new tool-manifest
    dotnet tool install DemaConsulting.CanvasNet.Tool

CanvasNet itself does not pin the tool in its manifest: its CI installs the package it has just built
as a local tool (dotnet tool install --local --add-source packages ...) before the documents are
built, and nothing is committed. For local development an environment variable replaces the command,
for example to try a freshly built tool without installing it. The value is a program and optional
leading arguments; whitespace separates them, double quotes group a part with spaces, and there are no
backslash escapes, so a Windows path needs none:

    CANVASNET_TOOL=dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll
    CANVASNET_TOOL="C:/Tools/canvasnet/canvasnet.exe"

File access. By default the filter passes no file-access mode, so a document reads its pictures, includes and data
as Pandoc reads its images: any regular local file. Two environment variables opt in to a restrictive mode for a
build of documents you do not fully trust:

    CANVASNET_FILE_ACCESS   roots, no-symlinks, or roots,no-symlinks (any is the default), passed as --file-access
                            to every render and flatten
    CANVASNET_ROOTS         folders separated like PATH (";" on Windows, ":" elsewhere), each passed as
                            --allow-root as an absolute path (a relative folder is taken from the working
                            directory); it implies roots

The mode roots without CANVASNET_ROOTS does not leave the folders to the tool: the filter passes, as --allow-root,
the folders the tool would take by default, the working directory and the picture's folder when it is outside it.
So the tool decides only from its arguments. These arguments are part of every picture's name, so a picture drawn
without the variables, or from another working directory, is not reused with them. Without --allow-root, roots
allows everything under the working directory; for documents you do not trust, set CANVASNET_ROOTS to the
documents' folders.

An unknown mode, or CANVASNET_ROOTS with CANVASNET_FILE_ACCESS=any or no-symlinks, stops the build before the tool
runs. In a restrictive mode a picture the mode denies (the tool's warning image-access-denied) stops the build, and
so does a denied include or data file (the tool's error file-access-denied). In a roots mode the filter itself never
opens a referenced file outside the folders while it hashes a picture's files for its name (compared by path; a link
is judged by the tool). A CI build can confine every picture to the documentation folders:

    CANVASNET_FILE_ACCESS=roots CANVASNET_ROOTS="$PWD/docs:$PWD/assets" dotnet pandoc --defaults docs/x/definition.yaml
    (PowerShell: $env:CANVASNET_FILE_ACCESS = 'roots'; $env:CANVASNET_ROOTS = "$PWD\docs;$PWD\assets")

After rebuilding a local tool, whose version text may not change, delete docs/generated/canvasnet
if the pictures do not follow (a DLL beside a "dotnet ...Tool.dll" override is detected on its own).

The picture's src is written relative to the folder of the output file for an HTML writer, because
WeasyPrint, which turns that HTML into the PDF, reads the picture from there; with --embed-resources,
a non-HTML writer or no output file (standard output) it is a path Pandoc itself can read.

A source the tool cannot draw stops the build with the tool's own message (for a model it names the
file, line and column of a syntax error). Parts the tool leaves out (import(), surface(), offset(), a
remote chart URL) and the model's echo() lines are shown as warnings when the picture is drawn; the
build goes on.

Rendered pictures are build output: never commit them. Keep the sources under version control.

Provenance: generalized from the scad.lua filter of DEMA Consulting's DocPilot breadboard template,
with the same organization (header, helpers, hash, render, rewrite), to draw every input the tool reads.
]]

-- Part of every picture's hash, so a change to what the filter draws or how it treats a diagram draws the pictures
-- again. 12: a diagram type the tool does not draw is left as written with a warning, no longer for another filter.
-- 13: packet diagrams are drawn, so a marker that left one as written is no longer used.
-- 14: git graphs are drawn, so a marker that left one as written is no longer used.
-- 15: CANVASNET_FILE_ACCESS and CANVASNET_ROOTS pass --file-access and --allow-root (absolute folders; for roots
-- without CANVASNET_ROOTS, the working directory and the picture's folder) and are part of every name, and an
-- absolute path or a file: URI without a host is hashed, as the tool reads it.
-- 16: CANVASNET_ROOTS without CANVASNET_FILE_ACCESS passes --file-access roots explicitly, a restrictive build also
-- stops on the tool's file-access-denied diagnostic, and a chart linked back from a picture it draws is hashed once.
local FILTER_VERSION = "16"
local SCAD_DEFAULT_SIZE = "1600x1200"
local DEFAULT_BACKGROUND = "white"
-- The same limit DocPilot's preview uses, so a model that previews also builds.
local MAX_TRIANGLES = "2000000"
local MAX_PIXELS = 8192
local MAX_DEPTH = 64
local MAX_DEPENDENCIES = 512

-- File name ends, checked in this order.
local SUFFIXES = {
  { ".vl.json", "vegalite" },
  { ".vl", "vegalite" },
  { ".scad", "scad" },
  { ".vsdx", "vsdx" },
  { ".pptx", "pptx" },
  { ".pdf", "pdf" },
  { ".mmd", "mermaid" },
  { ".mermaid", "mermaid" },
}

-- The code a diagram the tool does not draw gets in its --json diagnostics (a 16x16 empty picture is written):
-- the filter leaves such a diagram as written and warns, so the document shows its code.
local UNSUPPORTED_DIAGRAM = "mermaid-diagram-unsupported"

local SCAD = { scad = true }
local MERMAID = { mermaid = true }
local THEMES = { default = true, base = true, dark = true, forest = true, neutral = true }
local PAGED = { vsdx = true, pptx = true, pdf = true }
local VIEWS = { iso = true, front = true, back = true, left = true, right = true, top = true, bottom = true }
local PROJECTIONS = { ortho = true, orthographic = true, perspective = true }

local function fail(message)
  error("canvasnet.lua: " .. message, 0)
end

local function warn(message)
  if pandoc.log and pandoc.log.warn then
    pandoc.log.warn(message)
  else
    io.stderr:write("[WARNING] " .. message .. "\n")
  end
end

local function read_file(path)
  local handle = io.open(path, "rb")
  if not handle then
    return nil
  end
  local content = handle:read("a")
  handle:close()
  return content
end

local function file_exists(path)
  local handle = io.open(path, "rb")
  if handle then
    handle:close()
    return true
  end
  return false
end

-- Collapses "." and ".." segments, which pandoc.path.normalize leaves in place.
local function collapse(path)
  local parts = {}
  local split = pandoc.path.split(pandoc.path.normalize(path))
  -- A rooted path's first part ("/", "C:\", a share) is its root, which a ".." above it leaves in place.
  local root = (split[1] and split[1]:match("[/\\]$")) and 1 or 0
  for _, part in ipairs(split) do
    if part == ".." and #parts == root and root == 1 then
      -- Climbing above the root stays at the root.
    elseif part == ".." and #parts > root and parts[#parts] ~= ".." then
      table.remove(parts)
    elseif part ~= "." then
      table.insert(parts, part)
    end
  end
  return pandoc.path.join(parts)
end

-- An absolute, collapsed path: a relative one is taken from the working directory.
local function absolute(path)
  if pandoc.path.is_absolute(path) then
    return collapse(path)
  end
  return collapse(pandoc.path.join({ pandoc.system.get_working_directory(), path }))
end

-- The path of "target" seen from the folder "from" (both absolute and collapsed), with forward slashes, or
-- nil when there is no relative path (another drive).
local function relative_path(from, target)
  local from_parts = pandoc.path.split(from)
  local target_parts = pandoc.path.split(target)
  local common = 0
  while common < #from_parts and common < #target_parts
    and from_parts[common + 1] == target_parts[common + 1] do
    common = common + 1
  end
  if common == 0 then
    return nil
  end
  local parts = {}
  for _ = common + 1, #from_parts do
    table.insert(parts, "..")
  end
  for index = common + 1, #target_parts do
    table.insert(parts, target_parts[index])
  end
  return (table.concat(parts, "/"):gsub("\\", "/"))
end

local function decode_percent(text)
  return (text:gsub("%%(%x%x)", function(hex) return string.char(tonumber(hex, 16)) end))
end

-- Which kind of input a picture file name is, or nil when the filter leaves the picture alone. A name with
-- a URL scheme (two or more letters before the colon, so a drive letter is not one) is not a local file.
local function kind_of(src)
  if src:match("^%a[%w+.-]+:") then
    return nil
  end
  local lower = src:lower()
  for _, entry in ipairs(SUFFIXES) do
    if lower:sub(-#entry[1]) == entry[1] then
      return entry[2]
    end
  end
  return nil
end

-- Splits the tool command into a program and its leading arguments. Whitespace separates; a double
-- quote groups (including spaces); there are no escapes, so a Windows path needs none.
local function split_command(text)
  local tokens = {}
  local current = {}
  local quoted = false
  local started = false
  local function finish()
    local token = table.concat(current)
    if token == "" then
      fail("CANVASNET_TOOL has an empty quoted part: " .. text)
    end
    table.insert(tokens, token)
    current = {}
    started = false
  end
  for char in text:gmatch(".") do
    if char == '"' then
      quoted = not quoted
      started = true
    elseif not quoted and char:match("%s") then
      if started then
        finish()
      end
    else
      table.insert(current, char)
      started = true
    end
  end
  if quoted then
    fail("CANVASNET_TOOL has a quote that is never closed: " .. text)
  end
  if started then
    finish()
  end
  if #tokens == 0 then
    fail("CANVASNET_TOOL names no program")
  end
  return tokens
end

-- The tool's --json document, or nil when the output is not one.
local function decode(output)
  if type(output) ~= "string" or output == "" then
    return nil
  end
  local ok, doc = pcall(pandoc.json.decode, output, false)
  if ok and type(doc) == "table" then
    return doc
  end
  return nil
end

-- The tool's own words for a failed run: its JSON error message (and hint), else whatever it printed.
local function describe_failure(err)
  if type(err) == "table" or type(err) == "userdata" then
    local ok, output = pcall(function() return err.output end)
    local doc = ok and decode(output) or nil
    if doc and type(doc.error) == "table" and doc.error.message then
      local message = tostring(doc.error.message)
      if doc.error.hint then
        message = message .. " (" .. tostring(doc.error.hint) .. ")"
      end
      return message
    end
    if ok and type(output) == "string" and output:match("%S") then
      return (output:gsub("%s+$", ""))
    end
    local has_code, code = pcall(function() return err.error_code end)
    if has_code and code then
      return "the tool exited with code " .. tostring(code) .. " and printed no message"
    end
  end
  return tostring(err)
end

-- The tool, found once per run: its program, leading arguments, and an identity that goes into every
-- picture's name. Asking for --version also proves that the tool can be started at all.
local tool = nil

local function find_tool()
  if tool then
    return tool
  end
  local override = os.getenv("CANVASNET_TOOL")
  local parts
  local label
  if override and override:match("%S") then
    parts = split_command(override)
    label = "CANVASNET_TOOL=" .. override
  else
    parts = { "dotnet", "canvasnet" }
    label = "default"
  end
  local program = table.remove(parts, 1)

  local version_args = {}
  for _, part in ipairs(parts) do
    table.insert(version_args, part)
  end
  table.insert(version_args, "--version")
  local ok, output = pcall(pandoc.pipe, program, version_args, "")
  if not ok then
    fail("cannot run the canvasnet tool ('" .. program .. " " .. table.concat(parts, " ") .. "'): "
      .. describe_failure(output) .. ". Pin DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json"
      .. " (dotnet tool install DemaConsulting.CanvasNet.Tool) or set CANVASNET_TOOL to the command to run.")
  end

  -- A local build ("dotnet path/Tool.dll") keeps its version text while its code changes: fingerprint the
  -- DLLs beside it, so a rebuilt tool draws the pictures again.
  local fingerprint = ""
  for _, part in ipairs(parts) do
    if part:lower():match("%.dll$") and file_exists(part) then
      local entries = {}
      local folder = pandoc.path.directory(absolute(part))
      for _, name in ipairs(pandoc.system.list_directory(folder)) do
        if name:lower():match("%.dll$") then
          local content = read_file(pandoc.path.join({ folder, name }))
          if content then
            table.insert(entries, name .. "=" .. pandoc.sha1(content))
          end
        end
      end
      table.sort(entries)
      fingerprint = table.concat(entries, ";")
      break
    end
  end

  tool = {
    program = program,
    leading = parts,
    identity = table.concat({ (output:gsub("%s+$", "")), label, fingerprint }, "\n"),
  }
  return tool
end

-- The modes CANVASNET_FILE_ACCESS may name, as the tool's --file-access reads them.
local FILE_ACCESS_MODES = {
  any = true,
  roots = true,
  ["no-symlinks"] = true,
  ["roots,no-symlinks"] = true,
  ["no-symlinks,roots"] = true,
}

-- The code of the tool's warning for a picture the file-access mode denies.
-- The codes of the tool's diagnostics for a file the file-access mode denies: a picture (image-access-denied) and an
-- include or data file (file-access-denied).
local ACCESS_DENIED = { ["image-access-denied"] = true, ["file-access-denied"] = true }

-- The file-access arguments of every render and flatten, read once from CANVASNET_FILE_ACCESS and CANVASNET_ROOTS:
-- "args" (--file-access and one --allow-root per folder, each an absolute path, a relative one taken from the
-- working directory), "restrictive" (a mode other than any, or folders), "default_roots" (the mode roots without
-- folders, whose folders file_access_of adds for each picture), and "key" (the arguments as part of every picture's
-- name). An unknown mode, or folders with the mode any or no-symlinks, stops the build before the tool runs.
local access = nil

local function find_file_access()
  if access then
    return access
  end
  local mode = os.getenv("CANVASNET_FILE_ACCESS")
  mode = mode and mode:match("^%s*(.-)%s*$"):lower():gsub("%s*,%s*", ",") or nil
  if mode == "" then
    mode = nil
  end
  if mode and not FILE_ACCESS_MODES[mode] then
    fail("CANVASNET_FILE_ACCESS is '" .. mode .. "'; use roots, no-symlinks, roots,no-symlinks, or any")
  end
  local roots = {}
  local list = os.getenv("CANVASNET_ROOTS")
  if list and list:match("%S") then
    for _, entry in ipairs(pandoc.path.split_search_path(list)) do
      if entry:match("%S") then
        table.insert(roots, absolute(entry))
      end
    end
  end
  if #roots > 0 and (mode == "any" or mode == "no-symlinks") then
    fail("CANVASNET_ROOTS cannot be used with CANVASNET_FILE_ACCESS=" .. mode .. "; use roots or roots,no-symlinks")
  end
  -- Folders imply roots; the mode is passed explicitly rather than left to the tool's default
  if #roots > 0 and not mode then
    mode = "roots"
  end
  local args = {}
  if mode then
    table.insert(args, "--file-access")
    table.insert(args, mode)
  end
  for _, root in ipairs(roots) do
    table.insert(args, "--allow-root")
    table.insert(args, root)
  end
  access = {
    args = args,
    roots = #roots > 0 and roots or nil,
    restrictive = #roots > 0 or (mode ~= nil and mode ~= "any"),
    default_roots = #roots == 0 and mode ~= nil and mode:find("roots", 1, true) ~= nil,
    key = "file-access\n" .. table.concat(args, "\n"),
  }
  return access
end

-- Whether the absolute, collapsed path "path" is the folder "folder" or inside it, comparing whole segments, with
-- the letter case ignored on Windows.
local function is_same_or_under(path, folder)
  local function plain(text)
    text = text:gsub("[/\\]+$", "")
    if pandoc.path.separator == "\\" then
      text = text:gsub("/", "\\"):lower()
    end
    return text
  end
  local inner = plain(path)
  local outer = plain(folder)
  return inner == outer or inner:sub(1, #outer + 1) == outer .. pandoc.path.separator
end

-- The file-access arguments and key of the picture drawn from "source". The mode roots without CANVASNET_ROOTS
-- names, as --allow-root, the folders the tool would take by default: the working directory, and the source's folder
-- when it is outside it. The tool's decision then depends only on its arguments, which are part of the picture's
-- name, so the same document built from another working directory is drawn again and never skips a denial.
local function file_access_of(source)
  local shared = find_file_access()
  if not shared.default_roots then
    return shared
  end
  local args = { table.unpack(shared.args) }
  local working = absolute(pandoc.system.get_working_directory())
  local folder = pandoc.path.directory(absolute(source))
  local roots = { working }
  table.insert(args, "--allow-root")
  table.insert(args, working)
  if not is_same_or_under(folder, working) then
    table.insert(args, "--allow-root")
    table.insert(args, folder)
    table.insert(roots, folder)
  end
  return {
    args = args,
    roots = roots,
    restrictive = shared.restrictive,
    key = "file-access\n" .. table.concat(args, "\n"),
  }
end

-- The folders of a roots mode the dependency walk of the picture being drawn may open files in, or nil for anywhere.
local walk_roots = nil

-- Whether the dependency walk takes the file "path" a picture references (absolute and collapsed): it exists and, in a
-- roots mode, is under one of the folders, compared by path. A file outside them is never opened; it is left out of
-- the hash and the tool denies it, which stops the build, so no picture that needs it is kept. A link is judged by
-- the tool alone: the walk may hash a file a link inside the folders leads to before the tool denies it.
local function dependency_exists(path)
  if walk_roots then
    local inside = false
    for _, root in ipairs(walk_roots) do
      if is_same_or_under(path, root) then
        inside = true
        break
      end
    end
    if not inside then
      return false
    end
  end
  return file_exists(path)
end

-- Stops the build for a picture the file-access mode denied, when CANVASNET_FILE_ACCESS or CANVASNET_ROOTS made the
-- mode restrictive: a document must not be published with a picture silently missing. The partial file is removed.
local function fail_on_denial(doc, name, partial)
  if not find_file_access().restrictive then
    return
  end
  for _, diagnostic in ipairs(doc.diagnostics or {}) do
    if ACCESS_DENIED[diagnostic.code] then
      os.remove(partial)
      fail("drawing '" .. name .. "' failed: " .. tostring(diagnostic.message)
        .. " (denied by CANVASNET_FILE_ACCESS/CANVASNET_ROOTS)")
    end
  end
end

-- docs/filters/canvasnet.lua -> docs/generated/canvasnet.
local filters_dir = pandoc.path.directory(absolute(PANDOC_SCRIPT_FILE))
local out_dir = pandoc.path.join({ pandoc.path.directory(filters_dir), "generated", "canvasnet" })

local function resource_dirs()
  local dirs = {}
  for _, dir in ipairs(PANDOC_STATE.resource_path or { "." }) do
    table.insert(dirs, absolute(dir))
  end
  return dirs
end

-- The source file as an absolute path, found the way Pandoc finds a picture: an absolute name is used if
-- the file opens; a relative name is joined to each resource-path folder in turn and the first file that
-- opens is taken, or nil. No containment check: the filter is not a security boundary (see the header).
local function locate_source(src)
  local names = { src }
  local decoded = decode_percent(src)
  if decoded ~= src then
    table.insert(names, decoded)
  end
  for _, name in ipairs(names) do
    if pandoc.path.is_absolute(name) then
      local candidate = collapse(name)
      if file_exists(candidate) then
        return candidate
      end
    else
      for _, dir in ipairs(resource_dirs()) do
        local candidate = collapse(pandoc.path.join({ dir, name }))
        if file_exists(candidate) then
          return candidate
        end
      end
    end
  end
  return nil
end

-- The source file, or an error that stops the build and names the picture when no file is found.
local function find_source(src)
  return locate_source(src) or fail("cannot find '" .. src .. "' in the document's resource path")
end

-- The text a dependency is hashed under: its path from the source's folder, so the picture's name does not
-- depend on where the repository is checked out (the full path when there is no relative one).
local function label(root, path)
  return relative_path(root, path) or path
end

-- Adds a model file, and every file it pulls in that exists, to the hash input: each path once (the exact
-- collapsed text), in the order met. Every "include <...>" and "use <...>" is followed, relative to the
-- including file's folder, an absolute name as it is; matching in comments too can only add a file to the
-- hash. An include cycle ends because each path is taken once; the depth and file-count limits end anything
-- else (a link loop, say) with an error, so nothing is silently left out of the hash.
local function add_scad_sources(root, path, seen, parts, depth)
  if seen[path] then
    return
  end
  seen[path] = true
  if depth > MAX_DEPTH then
    fail("'" .. path .. "' is included more than " .. MAX_DEPTH .. " levels deep")
  end
  local content = read_file(path)
  if not content then
    return
  end
  if #parts >= MAX_DEPENDENCIES then
    fail("the model and the files it includes are more than " .. MAX_DEPENDENCIES .. " files; the limit is "
      .. MAX_DEPENDENCIES .. ", counting the model (every file must be hashed so that a changed one is drawn again)")
  end
  table.insert(parts, label(root, path) .. "\n" .. content)
  local folder = pandoc.path.directory(path)
  for _, keyword in ipairs({ "include", "use" }) do
    for target in content:gmatch(keyword .. "%s*<%s*([^>\r\n]-)%s*>") do
      local next_path = collapse(pandoc.path.join({ folder, target }))
      if dependency_exists(next_path) then
        add_scad_sources(root, next_path, seen, parts, depth + 1)
      end
    end
  end
end

-- Collects every string "url" of a "data" object anywhere in a decoded specification: the top level,
-- layers, concatenations, facets and lookups all use that shape. A field that happens to be called
-- "data" can only add a file to the hash. A specification nested deeper than MAX_DEPTH stops the build
-- rather than leaving a deeper data url out of the hash, so a changed data file is always drawn again.
local function collect_data_urls(node, urls, depth)
  if depth > MAX_DEPTH then
    fail("the chart specification is nested more than " .. MAX_DEPTH .. " levels deep; every data url"
      .. " must be hashed so that a changed data file is drawn again")
  end
  for key, value in pairs(node) do
    if type(value) == "table" then
      if key == "data" and type(value.url) == "string" then
        urls[value.url] = true
      end
      collect_data_urls(value, urls, depth + 1)
    end
  end
end

-- The local path of a file: URI without a host (file:///x, file:/x, file:///C:/x), percent-decoded, or nil for a URI
-- with a host (localhost included) or another form: the tool's own rule (FileReferenceResolver.ParseFileUri).
local function file_uri_path(text)
  local rest = text:match("^[fF][iI][lL][eE]:(.*)$")
  if not rest then
    return nil
  end
  local path
  if rest:sub(1, 3) == "///" then
    path = rest:sub(3)
  elseif rest:sub(1, 1) == "/" and rest:sub(2, 2) ~= "/" then
    path = rest
  else
    return nil
  end
  path = decode_percent(path)
  if path:match("^[/\\][/\\]") then
    return nil
  end
  -- "/C:/x" is the drive path C:/x
  if path:match("^/%a:") then
    path = path:sub(2)
  end
  return path
end

-- Whether a url or link is remote, which the tool never reads: the tool's own rule (FeatureCatalog.IsRemoteDataUrl,
-- which the image links share) is two leading slashes or backslashes (a network or device path), a scheme of two or
-- more characters other than file (a letter, then letters, digits, "+", "-" or "."; one letter is a drive), or a file:
-- URI with a host. Everything else is a local path: relative, absolute, rooted, a drive path, or a file: URI without a
-- host.
local function is_remote_url(url)
  local text = url:gsub("^%s+", "")
  if text:match("^[/\\][/\\]") then
    return true
  end
  local scheme = text:match("^(%a[%w+.-]*):")
  if not scheme or #scheme <= 1 then
    return false
  end
  return scheme:lower() ~= "file" or file_uri_path(text) == nil
end

-- The file a url names: a file: URI without a host by its path; an absolute path as it is; a path rooted without a
-- drive on the drive of the folder; otherwise taken from the folder (a backslash is a slash); nil when blank or remote.
local function local_data_file(folder, url)
  if url:match("^%s*$") or is_remote_url(url) then
    return nil
  end
  local path = file_uri_path((url:gsub("^%s+", ""))) or url
  path = path:gsub("\\", "/")
  if pandoc.path.is_absolute(path) then
    return collapse(path)
  end
  if path:match("^/") then
    return collapse((folder:match("^%a:") or "") .. path)
  end
  return collapse(pandoc.path.join({ folder, path }))
end

-- Collects what the "url" channel of an image mark names in its subtree: the literal links of a "value" or a
-- "datum" (also those of a condition), and the "field" names whose values in the rows are links.
local function collect_url_channel(node, links, fields, depth)
  if depth > MAX_DEPTH then
    fail("the chart specification is nested more than " .. MAX_DEPTH .. " levels deep; every image url"
      .. " must be hashed so that a changed picture is drawn again")
  end
  for key, value in pairs(node) do
    if type(value) == "string" then
      if key == "value" or key == "datum" then
        links[value] = true
      elseif key == "field" then
        fields[value] = true
      end
    elseif type(value) == "table" then
      collect_url_channel(value, links, fields, depth + 1)
    end
  end
end

-- Collects the links of the pictures that a chart can draw wherever they are written in the specification:
-- the "url" of a mark definition, and the literal links and field names of a "url" channel. A field that
-- happens to be called "url" can only add a file to the hash.
local function collect_image_links(node, links, fields, depth)
  if depth > MAX_DEPTH then
    fail("the chart specification is nested more than " .. MAX_DEPTH .. " levels deep; every image url"
      .. " must be hashed so that a changed picture is drawn again")
  end
  for key, value in pairs(node) do
    if type(value) == "table" then
      if key == "mark" and type(value.url) == "string" then
        links[value.url] = true
      elseif key == "url" then
        collect_url_channel(value, links, fields, 0)
      end
      collect_image_links(value, links, fields, depth + 1)
    end
  end
end

-- Adds the links that the named fields hold in the rows of a table.
local function add_row_links(rows, links, fields)
  if type(rows) ~= "table" then
    return
  end
  for _, row in pairs(rows) do
    if type(row) == "table" then
      for field in pairs(fields) do
        if type(row[field]) == "string" then
          links[row[field]] = true
        end
      end
    end
  end
end

-- Adds the links that the named fields hold in the inline rows ("values" of a data object, and the arrays of
-- "datasets"). The rows of a data file, or a field that a transform computes, are not tracked.
local function collect_row_links(node, links, fields, depth)
  if depth > MAX_DEPTH then
    fail("the chart specification is nested more than " .. MAX_DEPTH .. " levels deep; every image url"
      .. " must be hashed so that a changed picture is drawn again")
  end
  for key, value in pairs(node) do
    if type(value) == "table" then
      if key == "values" then
        add_row_links(value, links, fields)
      elseif key == "datasets" then
        for _, rows in pairs(value) do
          add_row_links(rows, links, fields)
        end
      end
      collect_row_links(value, links, fields, depth + 1)
    end
  end
end
-- The file that a picture link of a chart names: the link without its fragment ("#page=2") and query, taken
-- from the specification's folder, or nil when it is blank or remote (which the tool never reads).
local function local_image_file(folder, link)
  return local_data_file(folder, (link:gsub("[#?].*$", "")))
end

-- Adds the picture files of a chart, a diagram or an SVG drawing to the hash input, so that a changed picture draws it
-- again: a model with the files it includes, a chart, a diagram or an SVG file that is a picture with its own files,
-- and any other file as it is. The pictures of a chart that come from a data file are not tracked (the documentation
-- says so).
local add_chart_sources
local add_mermaid_sources
local add_svg_sources

local function add_image_files(files, root, parts, seen, depth)
  if depth > MAX_DEPTH then
    fail("charts and diagrams are drawn inside each other more than " .. MAX_DEPTH .. " levels deep")
  end
  for _, file in ipairs(files) do
    if not seen[file] then
      local lower = file:lower()
      if lower:match("%.scad$") then
        add_scad_sources(root, file, seen, parts, 0)
      elseif lower:match("%.vl%.json$") or lower:match("%.vl$") then
        seen[file] = true
        add_chart_sources(file, pandoc.path.directory(file), parts, seen, depth + 1)
      elseif lower:match("%.mmd$") or lower:match("%.mermaid$") then
        add_mermaid_sources(file, root, parts, seen, depth + 1)
      elseif lower:match("%.svg$") then
        add_svg_sources(file, root, parts, seen, depth + 1)
      else
        seen[file] = true
        local data = read_file(file)
        if data then
          if #parts >= MAX_DEPENDENCIES then
            fail("the chart or diagram and the files it draws are more than " .. MAX_DEPENDENCIES
              .. " files; the limit is " .. MAX_DEPENDENCIES
              .. " (every file must be hashed so that a changed one is drawn again)")
          end
          table.insert(parts, label(root, file) .. "\n" .. data)
        end
      end
    end
  end
end

function add_chart_sources(path, root, parts, seen, depth)
  seen = seen or {}
  depth = depth or 0
  -- Taken before its links are followed, so a picture that links back to this chart does not read it again
  seen[path] = true
  local content = read_file(path)
  if not content then
    return
  end
  table.insert(parts, pandoc.path.filename(path) .. "\n" .. content)
  -- The tool ignores a leading UTF-8 byte order mark, so the data urls of such a file are hashed too.
  local ok, spec = pcall(pandoc.json.decode, (content:gsub("^\239\187\191", "")), false)
  if not ok or type(spec) ~= "table" then
    return -- the tool reports the syntax error
  end
  local urls = {}
  collect_data_urls(spec, urls, 0)
  local files = {}
  for url in pairs(urls) do
    local resolved = local_data_file(root, url)
    if resolved and dependency_exists(resolved) then
      table.insert(files, resolved)
    end
  end
  -- Sorted, so the order a JSON object is walked in cannot change the picture's name.
  table.sort(files)
  local unique = {}
  local unique_seen = {}
  for _, file in ipairs(files) do
    if not unique_seen[file] then
      unique_seen[file] = true
      table.insert(unique, file)
    end
  end
  -- A file left out of the hash could change without drawing the picture again, so more files than the
  -- limit is an error, never a silent cut-off.
  if #unique > MAX_DEPENDENCIES then
    fail("'" .. pandoc.path.filename(path) .. "' names " .. #unique .. " data files; the limit is "
      .. MAX_DEPENDENCIES .. " (every data file must be hashed so that a changed one is drawn again)")
  end
  for _, file in ipairs(unique) do
    local data = read_file(file)
    if data then
      table.insert(parts, label(root, file) .. "\n" .. data)
    end
  end
  -- The pictures of an image mark: the literal links and the links in the inline rows.
  local links = {}
  local fields = {}
  collect_image_links(spec, links, fields, 0)
  if next(fields) ~= nil then
    collect_row_links(spec, links, fields, 0)
  end
  local pictures = {}
  for link in pairs(links) do
    local resolved = local_image_file(root, link)
    if resolved and dependency_exists(resolved) then
      table.insert(pictures, resolved)
    end
  end
  table.sort(pictures)
  if #pictures > MAX_DEPENDENCIES then
    fail("'" .. pandoc.path.filename(path) .. "' names " .. #pictures .. " pictures; the limit is "
      .. MAX_DEPENDENCIES .. " (every picture must be hashed so that a changed one is drawn again)")
  end
  add_image_files(pictures, root, parts, seen, depth)
end

-- Collects the links of the pictures that a Mermaid diagram draws in its nodes: the img of a shape's data
-- (A@{ img: "part.png" }, quoted or not) and the src of an <img> tag in a label. Text that only looks like one (in
-- a comment, say) can only add a file to the hash.
local function collect_mermaid_links(content, links)
  for _, quote in ipairs({ '"', "'" }) do
    for link in content:gmatch("img%s*:%s*" .. quote .. "([^" .. quote .. "\r\n]*)" .. quote) do
      links[link] = true
    end
  end
  for link in content:gmatch("img%s*:%s*([^%s\"',}][^,}\r\n]*)") do
    links[(link:gsub("%s+$", ""))] = true
  end
  for tag in content:gmatch("<[iI][mM][gG][^>]*>") do
    for _, quote in ipairs({ '"', "'" }) do
      for link in tag:gmatch("[sS][rR][cC]%s*=%s*" .. quote .. "([^" .. quote .. "]*)" .. quote) do
        links[link] = true
      end
    end
    for link in tag:gmatch("[sS][rR][cC]%s*=%s*([^%s\"'>][^%s>]*)") do
      links[link] = true
    end
  end
end

-- Adds a Mermaid diagram and the local pictures its nodes name to the hash input: a link is taken from the
-- diagram's folder without its fragment ("#page=2", "#theme=dark") or query, a remote link (which the tool never
-- reads) is skipped (an absolute path and a file: URI without a host are files), and a linked model, chart or
-- diagram adds its own files. A diagram that names more than MAX_DEPENDENCIES different local pictures stops the
-- build, as for a chart.
function add_mermaid_sources(path, root, parts, seen, depth)
  seen[path] = true
  local content = read_file(path)
  if not content then
    return
  end
  if #parts >= MAX_DEPENDENCIES then
    fail("the diagram and the files it draws are more than " .. MAX_DEPENDENCIES .. " files; the limit is "
      .. MAX_DEPENDENCIES .. " (every file must be hashed so that a changed one is drawn again)")
  end
  table.insert(parts, label(root, path) .. "\n" .. content)
  local links = {}
  collect_mermaid_links(content, links)
  local folder = pandoc.path.directory(path)
  local pictures = {}
  local unique = {}
  for link in pairs(links) do
    for _, name in ipairs({ link, decode_percent(link) }) do
      local resolved = local_image_file(folder, name)
      if resolved and not unique[resolved] and dependency_exists(resolved) then
        unique[resolved] = true
        table.insert(pictures, resolved)
      end
    end
  end
  -- Sorted, so the order the links are met in cannot change the picture's name.
  table.sort(pictures)
  if #pictures > MAX_DEPENDENCIES then
    fail("'" .. pandoc.path.filename(path) .. "' names " .. #pictures .. " pictures; the limit is "
      .. MAX_DEPENDENCIES .. " (every picture must be hashed so that a changed one is drawn again)")
  end
  add_image_files(pictures, root, parts, seen, depth)
end

-- The five predefined XML entities; a character reference (&#N; or &#xN;) is decoded too.
local XML_ENTITIES = { amp = "&", lt = "<", gt = ">", quot = '"', apos = "'" }

local function decode_entities(text)
  return (text:gsub("&(#?[%w]+);", function(name)
    if XML_ENTITIES[name] then
      return XML_ENTITIES[name]
    end
    local code = nil
    if name:match("^#[xX]%x+$") then
      code = tonumber(name:sub(3), 16)
    elseif name:match("^#%d+$") then
      code = tonumber(name:sub(2))
    end
    if code and code < 0x110000 then
      return utf8.char(code)
    end
    return nil
  end))
end

-- Reads the attributes of a start tag from "pos" (just after the element name) up to its closing ">", a ">" inside a
-- quoted value (single or double quotes) being part of the value. Returns the attributes as a list of { name, value }
-- (an unquoted value, which is not XML, is read up to white space or ">"), the position after the tag, and whether the
-- tag closes itself ("/>").
local function read_attributes(content, pos)
  local attributes = {}
  local length = #content
  local closes = false
  while pos <= length do
    pos = content:find("[^%s]", pos) or (length + 1)
    local char = content:sub(pos, pos)
    if char == ">" then
      return attributes, pos + 1, closes
    elseif char == "/" then
      closes = true
      pos = pos + 1
    else
      closes = false
      local name = content:match("^[^%s=/>]+", pos)
      if not name then
        pos = pos + 1 -- a stray "=", which is not XML
      else
        pos = content:find("[^%s]", pos + #name) or (length + 1)
        local value = ""
        if content:sub(pos, pos) == "=" then
          pos = content:find("[^%s]", pos + 1) or (length + 1)
          local quote = content:sub(pos, pos)
          if quote == '"' or quote == "'" then
            local close = content:find(quote, pos + 1, true) or (length + 1)
            value = content:sub(pos + 1, close - 1)
            pos = close + 1
          else
            value = content:match("^[^%s>]*", pos)
            pos = pos + #value
          end
        end
        table.insert(attributes, { name, value })
      end
    end
  end
  return attributes, pos, closes
end

-- The namespace of the legacy xlink:href attribute.
local XLINK_NAMESPACE = "http://www.w3.org/1999/xlink"

-- The text that ends each kind of markup that holds no elements, by the text that starts it.
local SKIPPED_MARKUP = { { "<!--", "-->" }, { "<![CDATA[", "]]>" }, { "<?", "?>" } }

-- Collects the links of the image elements of an SVG text, as the tool finds them: for each element whose name is image
-- (with or without a namespace prefix), the unprefixed href, or the href in the XLink namespace (xlink:href, under any
-- prefix that is bound to that namespace on the element or an ancestor) when there is none; the value with entities
-- decoded and white space trimmed. Comments, character data, processing instructions, a DOCTYPE (with its internal
-- subset) and end tags hold no links, and a ">" inside a quoted attribute value does not end a tag.
local function collect_svg_links(content, links)
  local pos = 1
  local length = #content
  -- The namespace bindings of the open elements, innermost last: each a table of prefix to namespace
  local scopes = {}
  local function namespace_of(prefix, own)
    if own[prefix] then
      return own[prefix]
    end
    for i = #scopes, 1, -1 do
      if scopes[i][prefix] then
        return scopes[i][prefix]
      end
    end
    return nil
  end
  while true do
    local open = content:find("<", pos, true)
    if not open then
      return
    end
    local skipped = false
    for _, markup in ipairs(SKIPPED_MARKUP) do
      if content:sub(open, open + #markup[1] - 1) == markup[1] then
        local close = content:find(markup[2], open + #markup[1], true)
        if not close then
          return
        end
        pos = close + #markup[2]
        skipped = true
        break
      end
    end
    if not skipped then
      local next_char = content:sub(open + 1, open + 1)
      if next_char == "!" then
        -- A DOCTYPE or another declaration: up to the ">" outside quotes and outside its internal subset "[...]"
        local depth, quote, i = 0, nil, open + 2
        while i <= length do
          local char = content:sub(i, i)
          if quote then
            if char == quote then
              quote = nil
            end
          elseif char == '"' or char == "'" then
            quote = char
          elseif char == "[" then
            depth = depth + 1
          elseif char == "]" then
            depth = depth - 1
          elseif char == ">" and depth <= 0 then
            break
          end
          i = i + 1
        end
        pos = i + 1
      elseif next_char == "/" then
        pos = (content:find(">", open + 2, true) or length) + 1
        table.remove(scopes)
      else
        local name = content:match("^[%w_%.%-:]+", open + 1)
        if not name then
          pos = open + 1
        else
          local attributes, closes
          attributes, pos, closes = read_attributes(content, open + 1 + #name)
          local own = {}
          for _, attribute in ipairs(attributes) do
            local prefix = attribute[1]:match("^xmlns:(.+)$")
            if prefix then
              own[prefix] = decode_entities(attribute[2])
            end
          end
          if name == "image" or name:match(":image$") then
            local plain, legacy = nil, nil
            for _, attribute in ipairs(attributes) do
              local prefix = attribute[1]:match("^([^:]+):href$")
              if attribute[1] == "href" then
                plain = attribute[2]
              elseif legacy == nil and prefix and namespace_of(prefix, own) == XLINK_NAMESPACE then
                legacy = attribute[2]
              end
            end
            local link = plain or legacy
            if link then
              links[(decode_entities(link):gsub("^%s+", ""):gsub("%s+$", ""))] = true
            end
          end
          if not closes then
            table.insert(scopes, own)
          end
        end
      end
    end
  end
end

-- Adds an SVG drawing and the local files its image elements link to the hash input, and tells whether it links a
-- file that only the tool draws (a model, chart, diagram, or document page), directly or through a nested .svg file.
-- A link is taken from the drawing's folder without its fragment or query, a remote or data: link (which the tool
-- never reads) is skipped (an absolute path and a file: URI without a host are files), a nested .svg file is
-- followed with its own links, and a linked model, chart or diagram adds its own files. A link to such a file that
-- is missing makes the drawing composite too (the tool reports it) and adds a "missing" entry to the hash. While a
-- drawing is being read its entry in "seen" is true, and afterwards "composite" or "plain", so a drawing linked
-- twice is read once and a cycle ends. More than MAX_DEPENDENCIES files stops the build.
function add_svg_sources(path, root, parts, seen, depth)
  if depth > MAX_DEPTH then
    fail("SVG drawings are linked inside each other more than " .. MAX_DEPTH .. " levels deep")
  end
  seen[path] = true
  local content = read_file(path)
  if not content then
    seen[path] = "plain"
    return false
  end
  if #parts >= MAX_DEPENDENCIES then
    fail("the drawing and the files it links are more than " .. MAX_DEPENDENCIES .. " files; the limit is "
      .. MAX_DEPENDENCIES .. " (every file must be hashed so that a changed one is drawn again)")
  end
  table.insert(parts, label(root, path) .. "\n" .. content)
  local links = {}
  collect_svg_links(content, links)
  local folder = pandoc.path.directory(path)
  local files = {}
  local missing = {}
  local unique = {}
  for link in pairs(links) do
    -- The file a link names is read as the tool's resolver reads it: the query and the fragment are cut off first, then
    -- what is left is percent-decoded (so "flow%2Emmd" is flow.mmd and "a%23b.mmd" is the file a#b.mmd); a remote or
    -- data: link is skipped. A link to a file only the tool draws that is missing still makes the drawing one to
    -- flatten, so the tool reports the link instead of leaving it broken
    local reference = link:gsub("[#?].*$", "")
    local resolved
    if reference:match("^%s*[fF][iI][lL][eE]:") then
      -- A file: URI is percent-decoded as it is read
      resolved = local_data_file(folder, reference)
    else
      resolved = local_data_file(folder, reference) and local_data_file(folder, decode_percent(reference))
    end
    if resolved and not unique[resolved] then
      if dependency_exists(resolved) then
        unique[resolved] = true
        table.insert(files, resolved)
      elseif kind_of(resolved) ~= nil then
        unique[resolved] = true
        table.insert(missing, resolved)
      end
    end
  end
  -- Sorted, so the order the links are met in cannot change the picture's name.
  table.sort(files)
  table.sort(missing)
  if #files + #missing > MAX_DEPENDENCIES then
    fail("'" .. pandoc.path.filename(path) .. "' links " .. (#files + #missing) .. " files; the limit is "
      .. MAX_DEPENDENCIES .. " (every file must be hashed so that a changed one is drawn again)")
  end
  -- A missing file cannot be hashed, so the hash notes it as missing: creating it later changes the hash
  for _, file in ipairs(missing) do
    table.insert(parts, "missing " .. label(root, file))
  end
  local composite = #missing > 0
  for _, file in ipairs(files) do
    if file:lower():match("%.svg$") then
      if not seen[file] then
        add_svg_sources(file, root, parts, seen, depth + 1)
      end
      composite = composite or seen[file] == "composite"
    else
      composite = composite or kind_of(file) ~= nil
      add_image_files({ file }, root, parts, seen, depth + 1)
    end
  end
  seen[path] = composite and "composite" or "plain"
  return composite
end

-- Attribute checks. Each returns the normalized value, or stops the build with a message that names the
-- picture, the attribute and what is expected.
local function plain_text(src, key, value)
  if value == "" or value:find("%c") then
    fail("'" .. src .. "' has " .. key .. "=" .. value .. "; the value must be text without control characters")
  end
end

local function number_value(src, key, value, minimum, maximum, exclusive, whole)
  local pattern_ok = value:match("^%-?%d+$") or value:match("^%-?%d+%.%d+$") or value:match("^%-?%.%d+$")
  local number = pattern_ok and tonumber(value) or nil
  local expected = whole and "a whole number" or "a number"
  if not number or (whole and number ~= math.floor(number))
    or (exclusive and number <= minimum) or (not exclusive and number < minimum) or number > maximum then
    fail("'" .. src .. "' has " .. key .. "=" .. value .. "; write " .. expected
      .. (exclusive and " above " or " from ") .. tostring(minimum) .. " up to " .. tostring(maximum))
  end
  return string.format("%.15g", number)
end

local function size_value(src, value)
  local width, height = value:match("^(%d+)x(%d+)$")
  if not width or tonumber(width) < 1 or tonumber(height) < 1
    or tonumber(width) > MAX_PIXELS or tonumber(height) > MAX_PIXELS then
    fail("'" .. src .. "' has size=" .. value .. "; write WIDTHxHEIGHT in pixels (each 1 to " .. MAX_PIXELS
      .. "), for example 1600x1200")
  end
  return tostring(tonumber(width)) .. "x" .. tostring(tonumber(height))
end

local function background_value(src, value)
  if value ~= "transparent" and not value:match("^#%x%x%x%x%x%x$") and not value:match("^#%x%x%x%x%x%x%x%x$")
    and not value:match("^%a+$") then
    fail("'" .. src .. "' has background=" .. value .. "; write a color such as #F0F0F0, a name such as white,"
      .. " or transparent")
  end
  return value
end

-- Attributes that select a drawing option: the input types each applies to, and its check.
local OPTIONS = {
  size = { check = size_value },
  scale = { check = function(src, value) return number_value(src, "scale", value, 0.001, 8192, false, false) end },
  background = { check = background_value },
  page = {
    kinds = PAGED,
    applies = ".vsdx, .pptx and .pdf files",
    check = function(src, value)
      local page = value:match("^%d+$") and tonumber(value) or nil
      if not page or page < 1 then
        fail("'" .. src .. "' has page=" .. value .. "; write the page number, counting from 1")
      end
      return tostring(page)
    end,
  },
  dpi = {
    kinds = PAGED,
    applies = ".vsdx, .pptx and .pdf files",
    check = function(src, value, kind)
      return number_value(src, "dpi", value, 0, 2400, true, kind == "vsdx")
    end,
  },
  view = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value)
      local view = value:lower()
      if not VIEWS[view] then
        fail("'" .. src .. "' has view=" .. value .. "; write iso, front, back, left, right, top or bottom")
      end
      return view
    end,
  },
  azimuth = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value) return number_value(src, "azimuth", value, -360000, 360000, false, false) end,
  },
  elevation = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value) return number_value(src, "elevation", value, -90, 90, false, false) end,
  },
  projection = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value)
      local projection = value:lower()
      if not PROJECTIONS[projection] then
        fail("'" .. src .. "' has projection=" .. value .. "; write ortho or perspective")
      end
      return projection
    end,
  },
  theme = {
    kinds = MERMAID,
    applies = "Mermaid diagrams",
    check = function(src, value)
      local theme = value:lower()
      if not THEMES[theme] then
        fail("'" .. src .. "' has theme=" .. value .. "; write default, base, dark, forest or neutral")
      end
      return theme
    end,
  },
}

-- The tool options a picture's attributes ask for, as sorted "--name=value" texts (one argument each,
-- so a value is never taken for another option), and the attributes that stay on the picture.
local function build_options(src, kind, attributes)
  local values = {}
  local defines = {}
  local params = {}
  local keep = {}
  for key, value in pairs(attributes) do
    local name = key:match("^define%-(.+)$")
    local param = key:match("^param%-(.+)$")
    if param then
      if kind ~= "vegalite" then
        fail("'" .. src .. "' has " .. key .. "=" .. value .. ", but param-NAME applies to Vega-Lite charts only")
      end
      if param:find("=", 1, true) then
        fail("'" .. src .. "' has " .. key .. "; the parameter name cannot contain an equals sign")
      end
      plain_text(src, key, value)
      table.insert(params, "--param=" .. param .. "=" .. value)
    elseif name then
      if kind ~= "scad" then
        fail("'" .. src .. "' has " .. key .. "=" .. value .. ", but define-NAME applies to .scad models only")
      end
      if not name:match("^[%a_][%w_]*$") then
        fail("'" .. src .. "' has " .. key .. "; the variable name must be letters, digits and underscores,"
          .. " not starting with a digit")
      end
      plain_text(src, key, value)
      table.insert(defines, "--define=" .. name .. "=" .. value)
    elseif OPTIONS[key] then
      local option = OPTIONS[key]
      if option.kinds and not option.kinds[kind] then
        fail("'" .. src .. "' has " .. key .. "=" .. value .. ", but " .. key .. " applies to "
          .. option.applies .. " only")
      end
      plain_text(src, key, value)
      values[key] = option.check(src, value, kind)
    else
      keep[key] = value
    end
  end

  if values.size and values.scale then
    fail("'" .. src .. "' has both size and scale; use one of them")
  end
  if kind == "scad" then
    if not values.size and not values.scale then
      values.size = SCAD_DEFAULT_SIZE
    end
    values.background = values.background or DEFAULT_BACKGROUND
    values["max-triangles"] = MAX_TRIANGLES
  end
  if PAGED[kind] then
    values.page = values.page or "1"
  end

  local options = {}
  for key, value in pairs(values) do
    table.insert(options, "--" .. key .. "=" .. value)
  end
  for _, define in ipairs(defines) do
    table.insert(options, define)
  end
  for _, param in ipairs(params) do
    table.insert(options, param)
  end
  -- Sorted, so the same attributes in another order give the same picture name.
  table.sort(options)
  return options, keep
end

local HTML_WRITERS = { "^html", "^revealjs", "^slidy", "^slideous", "^s5", "^dzslides" }

-- The src a picture gets. An HTML writer is read back by WeasyPrint relative to the output file, so the
-- path is written from the output file's folder; otherwise it is a path Pandoc itself can read.
local function picture_src(out_file)
  local embedded = false
  if PANDOC_WRITER_OPTIONS then
    local ok, value = pcall(function() return PANDOC_WRITER_OPTIONS.embed_resources end)
    embedded = ok and value and true or false
  end
  local html = false
  for _, pattern in ipairs(HTML_WRITERS) do
    if (FORMAT or ""):match(pattern) then
      html = true
    end
  end
  local output_file = PANDOC_STATE.output_file
  if html and not embedded and output_file and output_file ~= "" then
    local relative = relative_path(pandoc.path.directory(absolute(output_file)), out_file)
    if relative then
      return relative
    end
  end
  return (out_file:gsub("\\", "/"))
end

math.randomseed(os.time() + math.floor(os.clock() * 1000000))

-- Writes a text file under a temporary name and then renames it, so a build running at the same time never reads
-- a half-written file. A file that is there already holds the same text (its name is a hash of it).
local function write_atomically(path, content)
  if file_exists(path) then
    return
  end
  local partial = path .. "." .. tostring(math.random(100000000, 999999999)) .. ".partial"
  local handle = io.open(partial, "wb")
  if not handle then
    fail("could not write " .. partial)
  end
  handle:write(content)
  handle:close()
  if not os.rename(partial, path) then
    -- Another build wrote the same file first.
    os.remove(partial)
    if not file_exists(path) then
      fail("could not write " .. path)
    end
  end
end

-- Draws a source file into the cache folder, unless it is there already, and returns the picture's file and the
-- attributes that stay on the picture. A diagram of a type the tool does not draw returns nil (it is left as
-- written): the tool's message is a warning, and a marker in the cache that holds it saves
-- later builds from starting the tool for it again.
local function draw(name, source, kind, attributes, stem)
  local options, keep = build_options(name, kind, attributes)
  local command = find_tool()
  local file_access = file_access_of(source)

  local root = pandoc.path.directory(source)
  local sources = {}
  walk_roots = file_access.roots
  if kind == "scad" then
    add_scad_sources(root, source, {}, sources, 0)
  elseif kind == "vegalite" then
    add_chart_sources(source, root, sources)
  elseif kind == "mermaid" then
    add_mermaid_sources(source, root, sources, {}, 0)
  else
    local content = read_file(source)
    if not content then
      fail("cannot read '" .. name .. "'")
    end
    table.insert(sources, pandoc.path.filename(source) .. "\n" .. content)
  end
  walk_roots = nil

  local key = pandoc.sha1(table.concat({
    "canvasnet.lua " .. FILTER_VERSION,
    kind,
    command.identity,
    file_access.key,
    table.concat(options, ";"),
    table.concat(sources, "\n\0\n"),
  }, "\n\0\n"))

  pandoc.system.make_directory(out_dir, true)
  local out_file = pandoc.path.join({ out_dir, stem .. "-" .. key .. ".png" })
  local marker = pandoc.path.join({ out_dir, key .. ".unsupported" })
  local left_alone = "; it is left as written and not drawn"

  if file_exists(out_file) then
    return out_file, keep
  end
  local known = read_file(marker)
  if known then
    warn("canvasnet.lua: " .. name .. ": " .. known .. left_alone)
    return nil
  end

  local suffix = tostring(math.random(100000000, 999999999))
  local partial = pandoc.path.join({ out_dir, key .. "." .. suffix .. ".partial.png" })
  local args = {}
  for _, part in ipairs(command.leading) do
    table.insert(args, part)
  end
  table.insert(args, "render")
  table.insert(args, source)
  for _, option in ipairs(options) do
    table.insert(args, option)
  end
  for _, arg in ipairs(file_access.args) do
    table.insert(args, arg)
  end
  table.insert(args, "--output")
  table.insert(args, partial)
  table.insert(args, "--json")

  local ok, result = pcall(pandoc.pipe, command.program, args, "")
  if not ok then
    os.remove(partial)
    fail("drawing '" .. name .. "' failed: " .. describe_failure(result))
  end

  local doc = decode(result) or {}
  fail_on_denial(doc, name, partial)
  for _, diagnostic in ipairs(doc.diagnostics or {}) do
    -- Only the diagram itself being a type the tool does not draw leaves the picture alone; the same report about
    -- a picture embedded in it (which carries the chain of embedding files in "origin") is an ordinary note.
    local embedded = type(diagnostic.origin) == "table" and #diagnostic.origin > 0
    if kind == "mermaid" and diagnostic.code == UNSUPPORTED_DIAGRAM and not embedded then
      os.remove(partial)
      local message = tostring(diagnostic.message)
      write_atomically(marker, message)
      warn("canvasnet.lua: " .. name .. ": " .. message .. left_alone)
      return nil
    end
  end
  for _, diagnostic in ipairs(doc.diagnostics or {}) do
    warn("canvasnet.lua: " .. name .. ": " .. tostring(diagnostic.message))
  end
  for _, line in ipairs(doc.echo or {}) do
    warn("canvasnet.lua: " .. name .. ": " .. tostring(line))
  end

  if not os.rename(partial, out_file) then
    -- Another build drew the same picture first; its copy is identical.
    os.remove(partial)
    if not file_exists(out_file) then
      fail("could not write the picture of '" .. name .. "' to " .. out_file)
    end
  end
  return out_file, keep
end

-- The stem of a cache file name: the source's file name without its extension (and .vl), with every character that
-- is not a letter, a digit, "_" or "-" made a "-".
local function stem_of(source)
  local stem = pandoc.path.split_extension(pandoc.path.filename(source))
  return (stem:gsub("%.vl$", ""):gsub("[^%w_-]", "-"))
end

-- Whether a picture is a local SVG drawing.
local function is_svg(src)
  return not src:match("^%a[%w+.-]+:") and src:lower():match("%.svg$") ~= nil
end

-- An SVG drawing: left alone when it links no file only the tool draws; else the tool writes a flattened copy into
-- the cache folder, unless it is there already, and the picture points at it. Only scale is read; every other
-- attribute stays. When the tool finds nothing to flatten (a link the drawing never shows), a marker in the cache
-- saves later builds from starting it again and the picture is left alone.
local function flatten_svg(img)
  local name = img.src
  -- A drawing Pandoc cannot find is left to Pandoc, as before (it reports it)
  local source = locate_source(name)
  if not source then
    return nil
  end
  local root = pandoc.path.directory(source)
  local sources = {}
  local file_access = file_access_of(source)
  walk_roots = file_access.roots
  local composite = add_svg_sources(source, root, sources, {}, 0)
  walk_roots = nil
  if not composite then
    return nil
  end

  local options = {}
  local keep = {}
  for key, value in pairs(img.attributes) do
    if key == "scale" then
      plain_text(name, key, value)
      table.insert(options, "--scale=" .. OPTIONS.scale.check(name, value))
    else
      keep[key] = value
    end
  end

  local command = find_tool()
  -- The links the tool rebases in the copy lead from the cache folder to the drawing's folder, so the same drawing in
  -- another folder (or a renamed folder) is another copy. The folder is taken relative to the cache folder, so the
  -- name does not depend on where the repository is checked out (the full path when there is no relative one).
  local key = pandoc.sha1(table.concat({
    "canvasnet.lua " .. FILTER_VERSION,
    "svg",
    command.identity,
    file_access.key,
    table.concat(options, ";"),
    "folder " .. (relative_path(out_dir, root) or root),
    table.concat(sources, "\n\0\n"),
  }, "\n\0\n"))

  pandoc.system.make_directory(out_dir, true)
  local out_file = pandoc.path.join({ out_dir, stem_of(source) .. "-" .. key .. ".svg" })
  local marker = pandoc.path.join({ out_dir, key .. ".plain" })
  local known = read_file(marker)
  if known then
    -- The tool's warnings are kept in the marker, so a broken link is reported on every build, not only the first
    for line in known:gmatch("[^\n]+") do
      warn("canvasnet.lua: " .. name .. ": " .. line)
    end
    return nil
  end
  if not file_exists(out_file) then
    -- The copy is written in the cache folder itself, so the links the tool rebases are right after the rename
    local partial = pandoc.path.join({ out_dir, key .. "." .. tostring(math.random(100000000, 999999999)) .. ".partial.svg" })
    local args = {}
    for _, part in ipairs(command.leading) do
      table.insert(args, part)
    end
    for _, arg in ipairs({ "flatten", source, table.unpack(options) }) do
      table.insert(args, arg)
    end
    for _, arg in ipairs(file_access.args) do
      table.insert(args, arg)
    end
    for _, arg in ipairs({ "--output", partial, "--json" }) do
      table.insert(args, arg)
    end

    local ok, result = pcall(pandoc.pipe, command.program, args, "")
    if not ok then
      os.remove(partial)
      fail("flattening '" .. name .. "' failed: " .. describe_failure(result))
    end

    local doc = decode(result) or {}
    -- A denial stops the build before a marker or a copy is written
    fail_on_denial(doc, name, partial)
    local warnings = {}
    for _, diagnostic in ipairs(doc.diagnostics or {}) do
      local message = tostring(diagnostic.message):gsub("[\r\n]+", " ")
      table.insert(warnings, message)
      warn("canvasnet.lua: " .. name .. ": " .. message)
    end
    for _, line in ipairs(doc.echo or {}) do
      warn("canvasnet.lua: " .. name .. ": " .. tostring(line))
    end
    if doc.flattened ~= true then
      os.remove(partial)
      write_atomically(marker, table.concat(warnings, "\n") .. "\n")
      return nil
    end
    if not os.rename(partial, out_file) then
      -- Another build wrote the same copy first; it is identical.
      os.remove(partial)
      if not file_exists(out_file) then
        fail("could not write the flattened copy of '" .. name .. "' to " .. out_file)
      end
    end
  end

  img.src = picture_src(out_file)
  img.attributes = keep
  return img
end

function Image(img)
  if is_svg(img.src) then
    return flatten_svg(img)
  end
  local kind = kind_of(img.src)
  if not kind then
    return nil
  end

  local source = find_source(img.src)
  local out_file, keep = draw(img.src, source, kind, img.attributes, stem_of(source))
  if not out_file then
    return nil
  end

  img.src = picture_src(out_file)
  img.attributes = keep
  return img
end

-- The name a message gives a code block: its identifier, else its first line.
local function block_name(block)
  if block.identifier ~= "" then
    return "mermaid block #" .. block.identifier
  end
  local first = (block.text:match("^%s*([^\r\n]*)") or ""):gsub("%s+$", "")
  local length = utf8.len(first)
  if length and length > 40 then
    first = first:sub(1, utf8.offset(first, 41) - 1) .. "..."
  end
  return "mermaid block (" .. first .. ")"
end

-- A fenced code block of class mermaid: the text is written to the cache folder as <hash>.mmd and drawn like a
-- .mmd picture, and the block becomes the picture (a figure when it has a caption attribute). A diagram type the
-- tool does not draw leaves the block as it is, with a warning.
function CodeBlock(block)
  if not block.classes:includes("mermaid") then
    return nil
  end

  local name = block_name(block)
  local attributes = {}
  local caption = nil
  for key, value in pairs(block.attributes) do
    if key == "caption" then
      caption = value
    else
      attributes[key] = value
    end
  end

  pandoc.system.make_directory(out_dir, true)
  local text = block.text .. "\n"
  local source = pandoc.path.join({ out_dir, pandoc.sha1("canvasnet.lua mermaid block\n" .. text) .. ".mmd" })
  write_atomically(source, text)

  local out_file, keep = draw(name, source, "mermaid", attributes, "mermaid")
  if not out_file then
    return nil
  end

  local classes = {}
  for _, class in ipairs(block.classes) do
    if class ~= "mermaid" then
      table.insert(classes, class)
    end
  end
  local description = {}
  if caption then
    description = pandoc.utils.blocks_to_inlines(pandoc.read(caption, "markdown").blocks)
  end
  local src = picture_src(out_file)
  if caption then
    local image = pandoc.Image(description, src, "", pandoc.Attr("", classes, keep))
    return pandoc.Figure({ pandoc.Plain({ image }) }, { pandoc.Plain(description) }, pandoc.Attr(block.identifier))
  end
  return pandoc.Para({ pandoc.Image(description, src, "", pandoc.Attr(block.identifier, classes, keep)) })
end
