using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Buffers.Binary;

namespace Warwrought.Core;

/// <summary>
/// Writes explicitly ordered primitive values into a canonical binary SHA-256 input.
/// This intentionally has no object/JSON serializer entry point.
/// </summary>
public sealed class CanonicalDigestWriter : IDisposable
{
    private readonly MemoryStream _buffer = new();

    public void WriteByte(byte value) => _buffer.WriteByte(value);

    public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public void WriteInt32(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        _buffer.Write(bytes);
    }

    public void WriteInt64(long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        _buffer.Write(bytes);
    }

    public void WriteUInt64(ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        _buffer.Write(bytes);
    }

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(bytes.Length);
        _buffer.Write(bytes, 0, bytes.Length);
    }

    public void WriteSimulationVersion(SimulationVersion version)
    {
        WriteInt32(version.Major);
        WriteInt32(version.Minor);
    }

    public void WriteSimulationTick(SimulationTick tick) => WriteInt64(tick.Value);

    public void WriteSimPosition(SimPosition position)
    {
        WriteInt32(position.X);
        WriteInt32(position.Z);
    }

    public string ComputeSha256Hex()
    {
        var digest = SHA256.HashData(_buffer.ToArray());
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public void Dispose() => _buffer.Dispose();
}
