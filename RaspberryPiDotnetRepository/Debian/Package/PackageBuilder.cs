﻿using LibObjectFile.Ar;
using RaspberryPiDotnetRepository.Data.ControlMetadata;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Tar;
using TarWriter = Unfucked.Compression.Writers.Tar.TarWriter;

namespace RaspberryPiDotnetRepository.Debian.Package;

public interface PackageBuilder: IAsyncDisposable, IDisposable {

    CompressionLevel gzipCompressionLevel { get; set; }
    TarWriter data { get; }

    Task build(Control control, Stream output);

}

public class PackageBuilderImpl: PackageBuilder {

    public const string CONTROL_ARCHIVE_FILENAME = "control.tar.gz";

    public CompressionLevel gzipCompressionLevel { get; set; } = CompressionLevel.Default;
    public TarWriter data { get; }

    private readonly Stream     dataArchiveStream = new MemoryStream();
    private readonly GZipStream dataGzipStream;

    public PackageBuilderImpl() {
        dataGzipStream = new GZipStream(NonDisposingStream.Create(dataArchiveStream), CompressionMode.Compress, gzipCompressionLevel);
        data           = new TarWriter(dataGzipStream, new TarWriterOptions(CompressionType.None, true));
    }

    public async Task build(Control control, Stream output) {
        data.Dispose();
        await dataGzipStream.DisposeAsync();
        dataArchiveStream.Position = 0;

        await using Stream controlArchiveStream = new MemoryStream();
        using (IWriter controlArchiveWriter = WriterFactory.Open(controlArchiveStream, ArchiveType.Tar, new GZipWriterOptions { CompressionLevel = gzipCompressionLevel })) {
            await using Stream controlFileBuffer = control.serialize().ToByteStream();
            controlArchiveWriter.Write("./control", controlFileBuffer);
        }

        controlArchiveStream.Position = 0;

        ArArchiveFile debArchive = new() { Kind = ArArchiveKind.Common };
        debArchive.AddFile(new ArBinaryFile {
            Name   = "debian-binary",
            Stream = "2.0\n".ToByteStream()
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