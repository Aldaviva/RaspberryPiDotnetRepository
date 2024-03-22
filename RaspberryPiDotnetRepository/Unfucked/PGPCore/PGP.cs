// ReSharper disable InconsistentNaming

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PgpCore.Abstractions;
using PgpCore.Enums;
using System.Text;

namespace RaspberryPiDotnetRepository.Unfucked.PGPCore;

/// <summary>
/// Like <see cref="PgpCore.PGP"/>, but with the added ability to generate detached signatures (<c>gpg --sign --detach-sign --armor</c>)
/// </summary>
public interface IPGP: PgpCore.Abstractions.IPGP {

    Task DetachedSignAsync(Stream inputStream, Stream outputStream, IDictionary<string, string>? headers = null);

    Task<string> DetachedSignAsync(string input, IDictionary<string, string>? headers = null);

}

/// <inheritdoc cref="IPGP"/>
public class PGP: PgpCore.PGP, IPGP {

    public PGP() { }
    public PGP(IEncryptionKeys encryptionKeys): base(encryptionKeys) { }

    public async Task DetachedSignAsync(Stream inputStream, Stream outputStream, IDictionary<string, string>? headers = null) {
        if (EncryptionKeys == null) {
            throw new ArgumentException("EncryptionKeys");
        } else if (inputStream.Position != 0) {
            throw new ArgumentException("inputStream should be at start of stream");
        }

        headers ??= new Dictionary<string, string>();

        await using ArmoredOutputStream armoredOutputStream   = new(outputStream, headers);
        PgpSignatureGenerator           pgpSignatureGenerator = InitDetachedSignatureGenerator();
        int                             length;
        byte[]                          buf = new byte[65535];
        while ((length = await inputStream.ReadAsync(buf)) > 0) {
            pgpSignatureGenerator.Update(buf, 0, length);
        }

        await using BcpgOutputStream bcpgOutputStream = new(armoredOutputStream);
        pgpSignatureGenerator.Generate().Encode(bcpgOutputStream);
    }

    public async Task<string> DetachedSignAsync(string input, IDictionary<string, string>? headers = null) {
        headers ??= new Dictionary<string, string>();

        await using Stream inputStream  = input.ToStream();
        await using Stream outputStream = new MemoryStream();
        await DetachedSignAsync(inputStream, outputStream, headers);
        outputStream.Seek(0, SeekOrigin.Begin);
        using StreamReader outputStreamReader = new(outputStream, Encoding.UTF8);
        return await outputStreamReader.ReadToEndAsync();
    }
    /*
    /// <summary>
    /// Clear sign the provided string
    /// </summary>
    /// <param name="input">Plain string to be signed</param>
    /// <param name="headers">Optional headers to be added to the output</param>
    public new async Task<string> ClearSignAsync(
        string                       input,
        IDictionary<string, string>? headers = null) {
        headers ??= new Dictionary<string, string>();

        await using Stream inputStream  = input.ToStream();
        await using Stream outputStream = new MemoryStream();
        await ClearSignAsync(inputStream, outputStream, headers);
        outputStream.Seek(0, SeekOrigin.Begin);
        using StreamReader outputStreamReader = new(outputStream, Encoding.UTF8);
        return await outputStreamReader.ReadToEndAsync();
    }

    /// <summary>
    /// Clear sign the provided stream
    /// </summary>
    /// <param name="inputStream">Plain data stream to be signed</param>
    /// <param name="outputStream">Output PGP signed stream</param>
    /// <param name="headers">Optional headers to be added to the output</param>
    public new async Task ClearSignAsync(
        Stream                       inputStream,
        Stream                       outputStream,
        IDictionary<string, string>? headers = null) {
        if (EncryptionKeys == null) {
            throw new ArgumentException("EncryptionKeys");
        } else if (inputStream.Position != 0) {
            throw new ArgumentException("inputStream should be at start of stream");
        }

        headers ??= new Dictionary<string, string>();

        await OutputClearSignedAsync(inputStream, outputStream, headers);
    }

    private async Task OutputClearSignedAsync(Stream inputStream, Stream outputStream, IDictionary<string, string> headers) {
        using StreamReader              streamReader          = new(inputStream);
        await using ArmoredOutputStream armoredOutputStream   = new(outputStream, headers);
        PgpSignatureGenerator           pgpSignatureGenerator = InitClearSignatureGenerator(armoredOutputStream);

        while (streamReader.Peek() >= 0) {
            string line          = (await streamReader.ReadLineAsync())!;
            byte[] lineByteArray = Encoding.ASCII.GetBytes(line);
            // Does the line end with whitespace?
            // Trailing white space needs to be removed from the end of the document for a valid signature RFC 4880 Section 7.1
            string cleanLine          = line.TrimEnd();
            byte[] cleanLineByteArray = Encoding.ASCII.GetBytes(cleanLine);

            pgpSignatureGenerator.Update(cleanLineByteArray, 0, cleanLineByteArray.Length);
            await armoredOutputStream.WriteAsync(lineByteArray, 0, lineByteArray.Length);

            // Add a line break back to the stream
            // armoredOutputStream.Write((byte)'\r');
            armoredOutputStream.Write((byte) '\n');

            // Update signature with line breaks unless we're on the last line
            if (streamReader.Peek() >= 0) {
                // pgpSignatureGenerator.Update((byte)'\r');
                pgpSignatureGenerator.Update((byte) '\n');
            }
        }

        armoredOutputStream.EndClearText();

        BcpgOutputStream bcpgOutputStream = new(armoredOutputStream);
        pgpSignatureGenerator.Generate().Encode(bcpgOutputStream);
    }*/

    private PgpSignatureGenerator InitDetachedSignatureGenerator() {
        PublicKeyAlgorithmTag tag                   = EncryptionKeys.SigningSecretKey.PublicKey.Algorithm;
        PgpSignatureGenerator pgpSignatureGenerator = new(tag, HashAlgorithmTag);
        pgpSignatureGenerator.InitSign(PgpSignature.CanonicalTextDocument, EncryptionKeys.SigningPrivateKey);
        foreach (string userId in EncryptionKeys.SigningSecretKey.PublicKey.GetUserIds()) {
            PgpSignatureSubpacketGenerator subPacketGenerator = new();
            subPacketGenerator.SetSignerUserId(false, userId);
            pgpSignatureGenerator.SetHashedSubpackets(subPacketGenerator.Generate());
            // Just the first one!
            break;
        }

        return pgpSignatureGenerator;
    }

    private PgpSignatureGenerator InitClearSignatureGenerator(ArmoredOutputStream armoredOutputStream) {
        PublicKeyAlgorithmTag tag                   = EncryptionKeys.SigningSecretKey.PublicKey.Algorithm;
        PgpSignatureGenerator pgpSignatureGenerator = new(tag, HashAlgorithmTag);
        pgpSignatureGenerator.InitSign(PgpSignature.CanonicalTextDocument, EncryptionKeys.SigningPrivateKey);
        armoredOutputStream.BeginClearText(HashAlgorithmTag);
        foreach (string userId in EncryptionKeys.SigningSecretKey.PublicKey.GetUserIds()) {
            PgpSignatureSubpacketGenerator subPacketGenerator = new();
            subPacketGenerator.SetSignerUserId(false, userId);
            pgpSignatureGenerator.SetHashedSubpackets(subPacketGenerator.Generate());
            // Just the first one!
            break;
        }

        return pgpSignatureGenerator;
    }

    private Stream ChainCompressedOut(Stream encryptedOut) {
        if (CompressionAlgorithm != CompressionAlgorithmTag.Uncompressed) {
            PgpCompressedDataGenerator compressedDataGenerator = new(CompressionAlgorithmTag.Zip);
            return compressedDataGenerator.Open(encryptedOut);
        }

        return encryptedOut;
    }

    private char FileTypeToChar() => FileType switch {
        PGPFileType.UTF8 => PgpLiteralData.Utf8,
        PGPFileType.Text => PgpLiteralData.Text,
        _                => PgpLiteralData.Binary
    };

}