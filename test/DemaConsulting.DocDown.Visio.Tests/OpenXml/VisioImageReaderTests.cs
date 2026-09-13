using System.IO.Packaging;
using DocDown.Visio.OpenXml;

namespace DemaConsulting.DocDown.Visio.Tests.OpenXml;

/// <summary>
///     Unit tests for <see cref="VisioImageReader"/>, proving it resolves a drawing's embedded media
///     from page and master image relationships, records which page references each image, and
///     excludes the package thumbnail, which is furniture rather than embedded content.
/// </summary>
public class VisioImageReaderTests
{
    /// <summary>The Open Packaging relationship type of an embedded image.</summary>
    private const string ImageRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>The Open Packaging relationship type of a package thumbnail.</summary>
    private const string ThumbnailRelationshipType = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail";

    /// <summary>The URI of the page-contents part used by the fixtures.</summary>
    private const string PageContentsUri = "/visio/pages/page1.xml";

    /// <summary>
    ///     Proves a page's embedded image is yielded with its page recorded, while the package
    ///     thumbnail is excluded.
    /// </summary>
    [Fact]
    public void VisioImageReader_Collect_PageImageAndThumbnail_YieldsImageOnlyWithPage()
    {
        using var stream = BuildPageImagePackage();
        using var package = Package.Open(stream, FileMode.Open, FileAccess.Read);

        var collection = VisioImageReader.Collect(package, new Dictionary<string, int> { [PageContentsUri] = 1 });

        var image = Assert.Single(collection.Images);
        Assert.Equal("image/png", image.MediaType);
        Assert.EndsWith("image1.png", image.SourceRef, StringComparison.Ordinal);

        // The image is associated with the page that references it, not left orphan or flagged template
        Assert.Equal([1], image.SourcePages);
        Assert.False(image.ReferencedByTemplate);

        // The page's inline references point at the same media part, for linking at occurrence
        var refs = Assert.Contains(1, collection.PageImageRefs);
        var pageRef = Assert.Single(refs);
        Assert.Equal(image.SourceRef, pageRef.SourceRef);
    }

    /// <summary>
    ///     Proves a master's embedded image is flagged template-referenced rather than given a page.
    /// </summary>
    [Fact]
    public void VisioImageReader_Collect_MasterImage_FlagsTemplateWithoutPage()
    {
        using var stream = BuildMasterImagePackage();
        using var package = Package.Open(stream, FileMode.Open, FileAccess.Read);

        var collection = VisioImageReader.Collect(package, new Dictionary<string, int> { [PageContentsUri] = 1 });

        var image = Assert.Single(collection.Images);
        Assert.Null(image.SourcePages);
        Assert.True(image.ReferencedByTemplate);
        Assert.Empty(collection.PageImageRefs);
    }

    /// <summary>
    ///     Builds an in-memory Visio-shaped package with a page that references an embedded PNG under
    ///     <c>visio/media</c> and a package thumbnail under <c>docProps</c>.
    /// </summary>
    /// <returns>A seekable stream over the package bytes, positioned at the start.</returns>
    private static MemoryStream BuildPageImagePackage()
    {
        var stream = new MemoryStream();
        using (var package = Package.Open(stream, FileMode.Create, FileAccess.ReadWrite))
        {
            var page = package.CreatePart(
                new Uri(PageContentsUri, UriKind.Relative), "application/vnd.ms-visio.page+xml");
            WriteBytes(page, "<xml/>"u8.ToArray());

            var media = package.CreatePart(new Uri("/visio/media/image1.png", UriKind.Relative), "image/png");
            WriteBytes(media, [1, 2, 3, 4]);
            page.CreateRelationship(media.Uri, TargetMode.Internal, ImageRelationshipType);

            var thumbnail = package.CreatePart(new Uri("/docProps/thumbnail.emf", UriKind.Relative), "image/x-emf");
            WriteBytes(thumbnail, [9, 9, 9]);
            package.CreateRelationship(thumbnail.Uri, TargetMode.Internal, ThumbnailRelationshipType);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    ///     Builds an in-memory Visio-shaped package whose only embedded image is referenced from a
    ///     master part, so it must be flagged template-referenced rather than associated with a page.
    /// </summary>
    /// <returns>A seekable stream over the package bytes, positioned at the start.</returns>
    private static MemoryStream BuildMasterImagePackage()
    {
        var stream = new MemoryStream();
        using (var package = Package.Open(stream, FileMode.Create, FileAccess.ReadWrite))
        {
            var master = package.CreatePart(
                new Uri("/visio/masters/master1.xml", UriKind.Relative), "application/vnd.ms-visio.master+xml");
            WriteBytes(master, "<xml/>"u8.ToArray());

            var media = package.CreatePart(new Uri("/visio/media/image1.png", UriKind.Relative), "image/png");
            WriteBytes(media, [1, 2, 3, 4]);
            master.CreateRelationship(media.Uri, TargetMode.Internal, ImageRelationshipType);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    ///     Writes bytes into a freshly created package part.
    /// </summary>
    /// <param name="part">The part to write into.</param>
    /// <param name="bytes">The bytes to write.</param>
    private static void WriteBytes(PackagePart part, byte[] bytes)
    {
        using var partStream = part.GetStream(FileMode.Create, FileAccess.Write);
        partStream.Write(bytes, 0, bytes.Length);
    }
}
