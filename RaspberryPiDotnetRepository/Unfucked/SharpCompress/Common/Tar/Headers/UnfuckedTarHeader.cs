using SharpCompress;
using SharpCompress.Common;
using System.Buffers.Binary;
using System.Text;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace RaspberryPiDotnetRepository.Unfucked.SharpCompress.Common.Tar.Headers;

#nullable disable

/// <summary>
/// Like <c>SharpCompress.Common.Tar.Headers.TarHeader</c> except you're not prevented from setting the file mode, owner, and group.
/// </summary>
public class UnfuckedTarHeader(ArchiveEncoding archiveEncoding) {

    internal static readonly DateTime EPOCH = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal string Name { get; set; }
    internal string LinkName { get; set; }

    public long Mode { get; set; } = -1;
    public long UserId { get; set; }
    public long GroupId { get; set; }
    internal long Size { get; set; }
    internal DateTime LastModifiedTime { get; set; }
    public EntryType EntryType { get; set; }
    internal Stream PackedStream { get; set; }
    internal ArchiveEncoding ArchiveEncoding { get; } = archiveEncoding;

    internal const int BLOCK_SIZE = 512;

    protected internal virtual void Write(Stream output) {
        byte[] buffer = new byte[BLOCK_SIZE];

        WriteOctalBytes(Mode != -1 ? Mode : 511, buffer, 100, 8); // file mode fixed by Ben: not hardcoded to 0o777
        WriteOctalBytes(UserId, buffer, 108, 8);                  // owner ID fixed by Ben: not hardcoded to 0
        WriteOctalBytes(GroupId, buffer, 116, 8);                 // group ID fixed by Ben: not hardcoded to 0

        int nameByteCount = ArchiveEncoding.GetEncoding().GetByteCount(Name);
        if (nameByteCount > 100) {
            // Set mock filename and filetype to indicate the next block is the actual name of the file
            WriteStringBytes("././@LongLink", buffer, 0, 100);
            buffer[156] = (byte) EntryType.LongName;
            WriteOctalBytes(nameByteCount + 1, buffer, 124, 12);
        } else {
            WriteStringBytes(ArchiveEncoding.Encode(Name), buffer, 100);
            WriteOctalBytes(Size, buffer, 124, 12);
            long time = (long) (LastModifiedTime.ToUniversalTime() - EPOCH).TotalSeconds;
            WriteOctalBytes(time, buffer, 136, 12);
            buffer[156] = (byte) EntryType;

            if (Size >= 0x1FFFFFFFF) {
                Span<byte> bytes12 = stackalloc byte[12];
                BinaryPrimitives.WriteInt64BigEndian(bytes12.Slice(4), Size);
                bytes12[0] |= 0x80;
                bytes12.CopyTo(buffer.AsSpan(124));
            }

            // added by Ben: serialize symlinks
            if (EntryType == EntryType.SymLink) {
                WriteStringBytes(ArchiveEncoding.Encode(LinkName), buffer.AsSpan(157, 100), 100);
            }
        }

        int crc = RecalculateChecksum(buffer);
        WriteOctalBytes(crc, buffer, 148, 8);

        output.Write(buffer, 0, buffer.Length);

        if (nameByteCount > 100) {
            WriteLongFilenameHeader(output);
            // update to short name lower than 100 - [max bytes of one character].
            // subtracting bytes is needed because preventing infinite loop(example code is here).
            //
            // var bytes = Encoding.UTF8.GetBytes(new string(0x3042, 100));
            // var truncated = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes, 0, 100));
            //
            // and then infinite recursion is occured in WriteLongFilenameHeader because truncated.Length is 102.
            Name = ArchiveEncoding.Decode(
                ArchiveEncoding.Encode(Name),
                0,
                100 - ArchiveEncoding.GetEncoding().GetMaxByteCount(1)
            );
            Write(output);
        }
    }

    private void WriteLongFilenameHeader(Stream output) {
        byte[] nameBytes = ArchiveEncoding.Encode(Name);
        output.Write(nameBytes, 0, nameBytes.Length);

        // pad to multiple of BlockSize bytes, and make sure a terminating null is added
        int numPaddingBytes = BLOCK_SIZE - nameBytes.Length % BLOCK_SIZE;
        if (numPaddingBytes == 0) {
            numPaddingBytes = BLOCK_SIZE;
        }

        output.Write(stackalloc byte[numPaddingBytes]);
    }

    internal bool Read(BinaryReader reader) {
        byte[] buffer = ReadBlock(reader);
        if (buffer.Length == 0) {
            return false;
        }

        // for symlinks, additionally read the link name
        if (ReadEntryType(buffer) == EntryType.SymLink) {
            LinkName = ArchiveEncoding.Decode(buffer, 157, 100).TrimNulls();
        }

        if (ReadEntryType(buffer) == EntryType.LongName) {
            Name   = ReadLongName(reader, buffer);
            buffer = ReadBlock(reader);
        } else {
            Name = ArchiveEncoding.Decode(buffer, 0, 100).TrimNulls();
        }

        EntryType = ReadEntryType(buffer);
        Size      = ReadSize(buffer);

        Mode = ReadAsciiInt64Base8(buffer, 100, 7);
        if (EntryType == EntryType.Directory) {
            Mode |= 0b1_000_000_000;
        }

        UserId  = ReadAsciiInt64Base8oldGnu(buffer, 108, 7);
        GroupId = ReadAsciiInt64Base8oldGnu(buffer, 116, 7);
        long unixTimeStamp = ReadAsciiInt64Base8(buffer, 136, 11);
        LastModifiedTime = EPOCH.AddSeconds(unixTimeStamp).ToLocalTime();

        Magic = ArchiveEncoding.Decode(buffer, 257, 6).TrimNulls();

        if (!string.IsNullOrEmpty(Magic) && "ustar".Equals(Magic)) {
            string namePrefix = ArchiveEncoding.Decode(buffer, 345, 157);
            namePrefix = namePrefix.TrimNulls();
            if (!string.IsNullOrEmpty(namePrefix)) {
                Name = namePrefix + "/" + Name;
            }
        }

        if (EntryType != EntryType.LongName && Name.Length == 0) {
            return false;
        }

        return true;
    }

    private string ReadLongName(BinaryReader reader, byte[] buffer) {
        long   size                 = ReadSize(buffer);
        int    nameLength           = (int) size;
        byte[] nameBytes            = reader.ReadBytes(nameLength);
        int    remainingBytesToRead = BLOCK_SIZE - nameLength % BLOCK_SIZE;

        // Read the rest of the block and discard the data
        if (remainingBytesToRead < BLOCK_SIZE) {
            reader.ReadBytes(remainingBytesToRead);
        }

        return ArchiveEncoding.Decode(nameBytes, 0, nameBytes.Length).TrimNulls();
    }

    private static EntryType ReadEntryType(byte[] buffer) => (EntryType) buffer[156];

    private static long ReadSize(byte[] buffer) {
        if ((buffer[124] & 0x80) == 0x80) // if size in binary
        {
            return BinaryPrimitives.ReadInt64BigEndian(buffer.AsSpan(0x80));
        }

        return ReadAsciiInt64Base8(buffer, 124, 11);
    }

    private static byte[] ReadBlock(BinaryReader reader) {
        byte[] buffer = reader.ReadBytes(BLOCK_SIZE);

        if (buffer.Length != 0 && buffer.Length < BLOCK_SIZE) {
            throw new InvalidOperationException("Buffer is invalid size");
        }

        return buffer;
    }

    private static void WriteStringBytes(ReadOnlySpan<byte> name, Span<byte> buffer, int length) {
        name.CopyTo(buffer);
        int i = Math.Min(length, name.Length);
        buffer.Slice(i, length - i).Clear();
    }

    private static void WriteStringBytes(string name, byte[] buffer, int offset, int length) {
        int i;

        for (i = 0; i < length && i < name.Length; ++i) {
            buffer[offset + i] = (byte) name[i];
        }

        for (; i < length; ++i) {
            buffer[offset + i] = 0;
        }
    }

    private static void WriteOctalBytes(long value, byte[] buffer, int offset, int length) {
        string val   = Convert.ToString(value, 8);
        int    shift = length - val.Length - 1;
        for (int i = 0; i < shift; i++) {
            buffer[offset + i] = (byte) ' ';
        }

        for (int i = 0; i < val.Length; i++) {
            buffer[offset + i + shift] = (byte) val[i];
        }
    }

    private static int ReadAsciiInt32Base8(byte[] buffer, int offset, int count) {
        string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s)) {
            return 0;
        }

        return Convert.ToInt32(s, 8);
    }

    private static long ReadAsciiInt64Base8(byte[] buffer, int offset, int count) {
        string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s)) {
            return 0;
        }

        return Convert.ToInt64(s, 8);
    }

    private static long ReadAsciiInt64Base8oldGnu(byte[] buffer, int offset, int count) {
        if (buffer[offset] == 0x80 && buffer[offset + 1] == 0x00) {
            return (buffer[offset + 4] << 24)
                | (buffer[offset + 5] << 16)
                | (buffer[offset + 6] << 8)
                | buffer[offset + 7];
        }

        string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();

        if (string.IsNullOrEmpty(s)) {
            return 0;
        }

        return Convert.ToInt64(s, 8);
    }

    private static long ReadAsciiInt64(byte[] buffer, int offset, int count) {
        string s = Encoding.UTF8.GetString(buffer, offset, count).TrimNulls();
        if (string.IsNullOrEmpty(s)) {
            return 0;
        }

        return Convert.ToInt64(s);
    }

    private static readonly byte[] eightSpaces = {
        (byte) ' ',
        (byte) ' ',
        (byte) ' ',
        (byte) ' ',
        (byte) ' ',
        (byte) ' ',
        (byte) ' ',
        (byte) ' '
    };

    internal static int RecalculateChecksum(byte[] buf) {
        // Set default value for checksum. That is 8 spaces.
        eightSpaces.CopyTo(buf, 148);

        // Calculate checksum
        int headerChecksum = 0;
        foreach (byte b in buf) {
            headerChecksum += b;
        }

        return headerChecksum;
    }

    internal static int RecalculateAltChecksum(byte[] buf) {
        eightSpaces.CopyTo(buf, 148);
        int headerChecksum = 0;
        foreach (byte b in buf) {
            if ((b & 0x80) == 0x80) {
                headerChecksum -= b ^ 0x80;
            } else {
                headerChecksum += b;
            }
        }

        return headerChecksum;
    }

    public long? DataStartPosition { get; set; }

    public string Magic { get; set; }

}

public enum EntryType: byte {

    File                 = 0,
    OldFile              = (byte) '0',
    HardLink             = (byte) '1',
    SymLink              = (byte) '2',
    CharDevice           = (byte) '3',
    BlockDevice          = (byte) '4',
    Directory            = (byte) '5',
    Fifo                 = (byte) '6',
    LongLink             = (byte) 'K',
    LongName             = (byte) 'L',
    SparseFile           = (byte) 'S',
    VolumeHeader         = (byte) 'V',
    GlobalExtendedHeader = (byte) 'g'

}