using LibObjectFile.Ar;
using RaspberryPiDotnetRepository.Unfucked.SharpCompress.Writers.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using System.Text.RegularExpressions;

namespace RaspberryPiDotnetRepository;

public class DebPackageBuilder: IAsyncDisposable, IDisposable {

    public const string CONTROL_ARCHIVE_FILENAME = "control.tar.gz";

    public CompressionLevel gzipCompressionLevel { get; set; } = CompressionLevel.Default;
    public UnfuckedTarWriter data { get; }

    private readonly Stream     dataArchiveStream = new MemoryStream();
    private readonly GZipStream dataGzipStream;

    private string controlText = string.Empty;

    public DebPackageBuilder() {
        dataGzipStream = new GZipStream(NonDisposingStream.Create(dataArchiveStream), CompressionMode.Compress, gzipCompressionLevel);
        data           = new UnfuckedTarWriter(dataGzipStream, new TarWriterOptions(CompressionType.None, true));
    }

    public string control {
        get => controlText;
        set => controlText = Regex.Replace(value.Trim().ReplaceLineEndings("\n"), @"\n{2,}", "\n");
    }

    public async Task build(Stream output) {
        data.Dispose();
        await dataGzipStream.DisposeAsync();
        dataArchiveStream.Position = 0;

        await using Stream controlArchiveStream = new MemoryStream();
        using (IWriter controlArchiveWriter = WriterFactory.Open(controlArchiveStream, ArchiveType.Tar, new GZipWriterOptions { CompressionLevel = gzipCompressionLevel })) {
            await using Stream controlFileBuffer = (control + '\n').ToStream();
            controlArchiveWriter.Write("./control", controlFileBuffer);
        }

        controlArchiveStream.Position = 0;

        ArArchiveFile debArchive = new() { Kind = ArArchiveKind.Common };
        debArchive.AddFile(new ArBinaryFile {
            Name   = "debian-binary",
            Stream = "2.0\n".ToStream()
        });
        debArchive.AddFile(new ArBinaryFile {
            Name   = CONTROL_ARCHIVE_FILENAME,
            Stream = controlArchiveStream
        });
        debArchive.AddFile(new ArBinaryFile {
            Name   = "data.tar.gz",
            Stream = dataArchiveStream
        });

        debArchive.Write(output);
    }

    public static int o(string octal) => Convert.ToInt32(octal, 8);
    public static int o(int    octal) => o(octal.ToString());

    public void Dispose() {
        data.Dispose();
        dataGzipStream.Dispose();
        dataArchiveStream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync() {
        data.Dispose();
        await dataGzipStream.DisposeAsync();
        await dataArchiveStream.DisposeAsync();
        GC.SuppressFinalize(this);
    }

}