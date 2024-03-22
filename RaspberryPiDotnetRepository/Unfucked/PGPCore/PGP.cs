// ReSharper disable InconsistentNaming

using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PgpCore.Abstractions;
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

}