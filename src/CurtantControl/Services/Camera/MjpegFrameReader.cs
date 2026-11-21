using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CurtantControl.Services.Camera;

/// <summary>Extracts complete JPEGs without trusting multipart boundary formatting.</summary>
public static class MjpegFrameReader
{
    private enum State { Seek, Prefix, Marker, LengthHigh, LengthLow, Payload, Entropy, EntropyMarker }

    public static async Task CopyAsync(Stream input, Stream output, CancellationToken cancellationToken,
        int maximumFrameBytes = 8 * 1024 * 1024)
    {
        if (maximumFrameBytes < 4) throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
        using var frame = new MemoryStream();
        var buffer = new byte[16384];
        var state = State.Seek;
        var previousFf = false;
        var marker = 0;
        var remaining = 0;
        var frames = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            for (var index = 0; index < count; index++)
            {
                var value = buffer[index];
                if (state == State.Seek)
                {
                    if (previousFf && value == 0xd8)
                    {
                        frame.SetLength(0);
                        frame.Position = 0;
                        frame.WriteByte(0xff);
                        frame.WriteByte(0xd8);
                        state = State.Prefix;
                    }
                    previousFf = value == 0xff;
                    continue;
                }
                frame.WriteByte(value);
                if (frame.Length > maximumFrameBytes)
                    throw new InvalidDataException("Camera JPEG exceeded the maximum frame size.");
                switch (state)
                {
                    case State.Prefix:
                        if (value != 0xff) throw new InvalidDataException("Invalid JPEG marker from camera stream.");
                        state = State.Marker;
                        break;
                    case State.Entropy:
                        if (value == 0xff) state = State.EntropyMarker;
                        break;
                    case State.Marker:
                    case State.EntropyMarker:
                        if (value == 0xff) break;
                        if (state == State.EntropyMarker && (value == 0 || value is >= 0xd0 and <= 0xd7))
                        {
                            state = State.Entropy;
                            break;
                        }
                        if (value == 0xd9)
                        {
                            await output.WriteAsync(frame.GetBuffer().AsMemory(0, (int)frame.Length), cancellationToken).ConfigureAwait(false);
                            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                            frames++;
                            state = State.Seek;
                            previousFf = false;
                        }
                        else if (value == 0xd8)
                        {
                            frame.SetLength(0);
                            frame.Position = 0;
                            frame.WriteByte(0xff);
                            frame.WriteByte(0xd8);
                            state = State.Prefix;
                        }
                        else if (value == 1 || value is >= 0xd0 and <= 0xd7)
                            state = State.Prefix;
                        else
                        {
                            marker = value;
                            state = State.LengthHigh;
                        }
                        break;
                    case State.LengthHigh:
                        remaining = value << 8;
                        state = State.LengthLow;
                        break;
                    case State.LengthLow:
                        remaining = (remaining | value) - 2;
                        if (remaining < 0) throw new InvalidDataException("Invalid JPEG segment length.");
                        state = remaining > 0 ? State.Payload : marker == 0xda ? State.Entropy : State.Prefix;
                        break;
                    case State.Payload:
                        if (--remaining == 0) state = marker == 0xda ? State.Entropy : State.Prefix;
                        break;
                }
            }
        }
        if (frames == 0) throw new InvalidDataException("HTTP response contained no complete JPEG frames. Check /video_feed output.");
    }
}
