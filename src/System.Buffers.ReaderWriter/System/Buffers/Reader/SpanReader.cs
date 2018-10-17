using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Buffers
{
    public ref partial struct SpanReader<T> where T : unmanaged, IEquatable<T>
    {
        private bool _moreData;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SpanReader(in ReadOnlySpan<T> span)
        {
            CurrentSpan = span;
            UnreadSpan = span;
            Consumed = 0;
            _moreData = span.Length > 0;
        }

        public bool End => !_moreData;
        public long Consumed { get; private set; }
        public int CurrentSpanIndex => (int)Consumed;

        public ReadOnlySpan<T> CurrentSpan { get; private set; }
        public ReadOnlySpan<T> UnreadSpan { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T value)
        {
            if (_moreData)
            {
                value = UnreadSpan[0];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if (count < 0 || count > UnreadSpan.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
            }

            Consumed += count;
            UnreadSpan = UnreadSpan.Slice(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadTo(out ReadOnlySpan<T> span, T delimiter, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<T> remaining = UnreadSpan;
            int index = remaining.IndexOf(delimiter);
            span = index < 1 ? default : remaining.Slice(0, index);
            if (index != -1)
            {
                Advance(index + (advancePastDelimiter ? 1 : 0));
                return true;
            }

            return false;
        }
    }

    public static partial class SpanReaderExtensions
    {
        private const int MaxParseBytes = 128;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryRead<T>(ref this SpanReader<byte> reader, out T value) where T : unmanaged
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if (span.Length < sizeof(T))
            {
                value = default;
                return false;
            }

            value = MemoryMarshal.Read<T>(span);
            reader.Advance(sizeof(T));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(
            ref this SpanReader<byte> reader,
            out int value,
            out int bytesConsumed,
            char standardFormat = '\0')
        {
            if (!Utf8Parser.TryParse(reader.UnreadSpan, out value, out bytesConsumed, standardFormat))
                return false;

            if (bytesConsumed >= MaxParseBytes)
            {
                value = default;
                bytesConsumed = 0;
                return false;
            }

            reader.Advance(bytesConsumed);
            return true;
        }
    }
}
