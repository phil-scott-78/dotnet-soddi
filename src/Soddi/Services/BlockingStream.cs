// from https://stackoverflow.com/a/3729877

using System.Buffers;

namespace Soddi.Services;

public class BlockingStream : Stream
{
    record ByteWithLength(byte[] Data, int Length);

    private static readonly ArrayPool<byte> s_byteArrayPool = ArrayPool<byte>.Shared;
    private readonly BlockingCollection<ByteWithLength> _blocks;
    private byte[]? _currentBlock;
    private int _currentBlockIndex;
    private int _currentBlockLength;

    public BlockingStream()
    {
        _blocks = new BlockingCollection<ByteWithLength>();
    }

    public override bool CanTimeout { get { return false; } }
    public override bool CanRead { get { return true; } }
    public override bool CanSeek { get { return false; } }
    public override bool CanWrite { get { return true; } }
    public override long Length { get { throw new NotSupportedException(); } }
    public override void Flush() { }
    public long TotalBytesWritten { get; private set; }
    public long TotalBytesRead { get; private set; }

    public int WriteCount { get; private set; }

    public override long Position
    {
        get { throw new NotSupportedException(); }
        set { throw new NotSupportedException(); }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArgs(buffer, offset, count);

        var bytesRead = 0;
        while (true)
        {
            if (_currentBlock != null)
            {
                var copy = Math.Min(count - bytesRead, _currentBlockLength - _currentBlockIndex);
                Array.Copy(_currentBlock, _currentBlockIndex, buffer, offset + bytesRead, copy);
                _currentBlockIndex += copy;
                bytesRead += copy;

                if (_currentBlockLength <= _currentBlockIndex)
                {
                    s_byteArrayPool.Return(_currentBlock, true);
                    _currentBlock = null;
                    _currentBlockIndex = 0;
                }

                if (bytesRead == count)
                {
                    TotalBytesRead += bytesRead;

                    return bytesRead;
                }
            }

            if (_blocks.TryTake(out var current, Timeout.Infinite))
            {
                _currentBlock = current.Data;
                _currentBlockLength = current.Length;
            }
            else
            {
                return bytesRead;
            }
        }
    }


    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArgs(buffer, offset, count);

        var newBuf = s_byteArrayPool.Rent(count);
        Array.Copy(buffer, offset, newBuf, 0, count);
        _blocks.Add(new ByteWithLength(newBuf, count));
        TotalBytesWritten += count;
        WriteCount++;
    }

    protected override void Dispose(bool disposing)
    {
        if (_currentBlock != null)
        {
            s_byteArrayPool.Return(_currentBlock, true);
        }

        base.Dispose(disposing);
        if (disposing)
        {
            _blocks.Dispose();
        }
    }

    public override void Close()
    {
        CompleteWriting();
        base.Close();
    }

    public void CompleteWriting()
    {
        _blocks.CompleteAdding();
    }

    [AssertionMethod]
    private static void ValidateBufferArgs(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (buffer.Length - offset < count)
            throw new ArgumentException("buffer.Length - offset < count");
    }
}
