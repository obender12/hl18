using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace hl18
{
    // ASCII string without allocations
    public struct AString : IComparable<AString>, IEquatable<AString>, ICloneable
    {
        public static AString Empty = new AString();

        private readonly byte[] buffer;
        private readonly int start;
        private readonly int length;

        #region Constructors

        // constructor from byte[]
        public AString(byte[] buffer)
        {
            this.buffer = buffer;
            start = 0;
            length = buffer == null ? 0 : buffer.Length;
        }

        // constructor from byte[] slice
        public AString(byte[] buffer, int start, int length)
        {
            this.buffer = buffer;
            this.start = start;
            this.length = length;
        }

        // constructor from ASCII string
        public AString(string source)
        {
            if (source == null)
            {
                start = length = 0;
                buffer = null;
            }
            else
            {
                start = 0;
                buffer = Encoding.UTF8.GetBytes(source);
                length = buffer.Length;
                /*
                start = 0;
                length = source.Length;
                buffer = new byte[length];
                for (int i = 0; i < source.Length; i++)
                    buffer[i] = (byte)source[i];*/
            }
        }

        // constructor from a string with specified encoding
        public AString(string source, Encoding encoding)
        {
            start = 0;
            buffer = encoding.GetBytes(source);
            length = buffer.Length;
        }

        // constructor from ArraySegment
        public AString(ArraySegment<byte> segment)
        {
            buffer = segment.Array;
            start = segment.Offset;
            length = segment.Count;
        }

        // copy constructor
        public AString(AString source)
        {
            buffer = source.buffer;
            start = source.start;
            length = source.length;
        }

        public AString Duplicate()
        {
            if (length == 0)
                return new AString();
            var buf = new byte[length];
            Array.Copy(buffer, start, buf, 0, length);
            return new AString(buf, 0, length);
        }

        public object Clone() => Duplicate();

        #endregion


        #region Accessors

        // return the length of the string
        public int Length { get => length; }

        // return the offset in the buffer
        public int Offset { get => start; }

        public byte[] Buffer { get => buffer; }

        // get byte at the index
        public byte this[int index] { get => buffer[start + index]; }

        public bool IsEmpty { get => buffer == null || length == 0; }

        #endregion


        #region Comparisons

        // IComparable implementation
        public int CompareTo(AString other)
        {
            return AsReadOnlySpan().SequenceCompareTo(other);
        }

        public unsafe override int GetHashCode()
        {
            if (buffer == null || length == 0)
                return 0;
            int hc = length;
            fixed( byte* p = &buffer[start] )
                for (int i = 0; i < length; ++i)
                    hc = (hc * 314159 + p[i]);
            return hc;
        }

        public ReadOnlySpan<byte> AsReadOnlySpan()
        {
            return new ReadOnlySpan<byte>(buffer, start, length);
        }

        // IEquatable implementation
        public bool Equals(AString other)
        {
            return AsReadOnlySpan().SequenceEqual(other);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return buffer == null;
            return Equals((AString)obj);
        }

        // operators

        // compare to another instance
        public static bool operator ==(AString one, AString two) => one.Equals(two);
        public static bool operator !=(AString one, AString two) => !(one == two);

        // compare to string
        public static bool operator ==(AString one, string two)
        {
            if (one.IsEmpty)
                return string.IsNullOrEmpty(two);
            if (string.IsNullOrEmpty(two))
                return one.IsEmpty;
            if (one.Length != two.Length)
                return false;
            for (int i = 0; i < one.Length; i++)
                if (one[i] != (byte)two[i])
                    return false;
            return true;
        }
        public static bool operator !=(AString one, string two) => !(one == two);

        // warning! allocates new byte[] buffer
        public static AString operator+(AString one, AString two)
        {
            var buf = new byte[one.Length+two.Length];
            for (int i = 0; i < one.Length; i++)
                buf[i] = one[i];
            for (int i = 0; i < two.Length; i++)
                buf[i + one.Length] = two[i];
            return new AString(buf);
        }

        #endregion


        #region Manipulations


        public int IndexOf(byte target)
        {
            for (int i = 0; i < length; i++)
                if (this[i] == target)
                    return i;
            return -1;
        }

        public int IndexOf(char target) => IndexOf((byte)target);

        public int IndexOfCRLF()
        {
            for (int i = 0; i < length - 1; i++)
                if (this[i] == 13 && this[i + 1] == 10)
                    return i;
            return -1;
        }

        public int IndexOf(AString substr)
        {
            for (int i = 0; i <= length - substr.Length; i++)
            {
                bool same = true;
                for (int j = 0; j < substr.Length; j++)
                    if (this[i + j] != substr[j])
                    {
                        same = false;
                        break;
                    }
                if (same)
                    return i;
            }
            return -1;
        }

        public bool StartsWith(AString substr)
        {
            for (int j = 0; j < substr.Length; j++)
                if (this[j] != substr[j])
                    return false;
            return true;
        }

        public AString Substring(int from)
        {
            var count = length - from;
            if (from + count > start + length)
                count = start + length - from;
            if (count <= 0)
                return new AString();
            return new AString(buffer, start + from, count);
        }

        public AString Substring(int from, int count)
        {
            if (from + count > start + length)
                count = start + length - from;
            if (count <= 0)
                return new AString();
            return new AString(buffer, start + from, count);
        }

        public int SplitCount(byte splitter)
        {
            int count = 1;
            for (int i = 0; i < length; i++)
                if (buffer[start + i] == splitter)
                    count++;
            return count;
        }

        // split the string into the provided array
        public AString[] Split(byte splitter)
        {
            var splitCount = SplitCount(splitter);
            if (splitCount == 1)
                return new AString[] { this };
            var splits = new AString[splitCount];
            int current = 0;
            int from = 0;
            int count = 0;
            int i = 0;
            while (i < length)
                if (this[i] == splitter)
                {
                    splits[current++] = Substring(from, count);
                    from = ++i;
                    count = 0;
                }
                else
                {
                    i++;
                    count++;
                }
            splits[current] = Substring(from, count);
            return splits;
        }
        public AString[] Split(char c) => Split((byte)c);


        public int LineCount()
        {
            int count = 1;
            for (int i = 0; i < length - 1; i++)
                if (this[i] == 13 && this[i + 1] == 10)
                    count++;
            return count;
        }


        // split the string lines (with crlf splitter)
        public AString[] SplitLines()
        {
            var lineCount = LineCount();
            if (lineCount == 1)
                return new AString[] { this };
            var splits = new AString[lineCount];
            int current = 0;
            int from = 0;
            int count = 0;
            int i = 0;
            while (i < length)
                if (this[i] == 13 && this[i + 1] == 10)
                {
                    splits[current++] = Substring(from, count);
                    from = i + 2;
                    i += 2;
                    count = 0;
                }
                else
                {
                    i++;
                    count++;
                }
            splits[current] = Substring(from, count);
            return splits;
        }

        #endregion


        #region Conversions

        // implicitely convert from string to AString
        public static implicit operator AString(string str) => new AString(str);

        // implicitely convert from ArraySegment<byte> to AString
        public static implicit operator AString(ArraySegment<byte> seg) => new AString(seg);

        // implicitely convert from AString to ReadOnlySpan<byte>
        public static implicit operator ReadOnlySpan<byte>(AString astr) => new ReadOnlySpan<byte>(astr.buffer, astr.start, astr.length);

        // convert to string
        public override string ToString()
        {
            if (IsEmpty)
                return string.Empty;
            return Encoding.UTF8.GetString(buffer, start, length);
        }

        // convert to int: expecting digits and '-', ignoring spaces
        public bool TryToInt(out int rval)
        {
            rval = 0;
            bool minus = false;
            bool digitFound = false;
            for (int i = 0; i < length; i++)
                if (this[i] >= (byte)'0' && this[i] <= (byte)'9')
                {
                    digitFound = true;
                    rval = rval * 10 + this[i] - (byte)'0';
                }
                else
                if (this[i] == (byte)'-')
                    minus = true;
                else
                if( this[i] != (byte)' ')
                    return false;
            if (minus)
                rval = -rval;
            return digitFound;
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetCodePoint(char a, char b, char c, char d)
        {
            return (((((ToNumber(a) * 16) + ToNumber(b)) * 16) + ToNumber(c)) * 16) + ToNumber(d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int ToNumber(char x)
        {
            if ('0' <= x && x <= '9')
            {
                return x - '0';
            }
            else if ('a' <= x && x <= 'f')
            {
                return x - 'a' + 10;
            }
            else if ('A' <= x && x <= 'F')
            {
                return x - 'A' + 10;
            }
            throw new Exception("Invalid character in JSON escaping: " + x);
        }


        public unsafe AString InPlaceUnescape()
        {
            if (length == 0)
                return this;
            char* chars = stackalloc char[length];
            int count = 0;
            int i = 0;
            while (i < length)
                if (this[i] == (byte)'\\')
                {
                    i++;
                    switch ((char)this[i])
                    {
                        case '"':
                        case '\\':
                        case '/':
                            chars[count++] = (char)this[i];
                            break;
                        case 'b':
                            chars[count++] = '\b';
                            break;
                        case 'f':
                            chars[count++] = '\f';
                            break;
                        case 'n':
                            chars[count++] = '\n';
                            break;
                        case 'r':
                            chars[count++] = '\r';
                            break;
                        case 't':
                            chars[count++] = '\t';
                            break;
                        case 'u':
                            i++; // points to the first octet
                            var a = (char)this[i++];
                            var b = (char)this[i++];
                            var c = (char)this[i++];
                            var d = (char)this[i++];
                            var codepoint = GetCodePoint(a, b, c, d);
                            chars[count++] = (char)codepoint;
                            break;
                        default:
                            throw new Exception("Bad JSON escape sequence");
                    }
                }
                else
                {
                    // regular character, copy as is
                    chars[count++] = (char)this[i++];
                }

            // encode chars into UTF8 bytes replacing the original escaped bytes
            fixed (byte* buf = &buffer[start])
            {
                var utf8Count = Encoding.UTF8.GetBytes(chars, count, buf, length);
                return new AString(buffer, start, utf8Count);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HexToInt(char h) =>
            h >= '0' && h <= '9'
                ? h - '0'
                : h >= 'a' && h <= 'f'
                    ? h - 'a' + 10
                    : h >= 'A' && h <= 'F'
                        ? h - 'A' + 10
                        : -1;


        public unsafe AString InPlaceUrlDecode()
        {
            if (length == 0)
                return this;
            int count = 0;

            // go through the bytes collapsing %XX and %uXXXX and appending
            // each byte as byte, with exception of %uXXXX constructs that
            // are appended as chars

            for (int i = 0; i < length; i++)
            {
                int pos = start + i;
                byte b = buffer[pos];

                // The code assumes that + and % cannot be in multibyte sequence

                if (b == '+')
                {
                    b = (byte)' ';
                }
                else if (b == '%' && i < length - 2)
                {
                    if (buffer[pos + 1] == 'u' && i < length - 5)
                    {
                        int h1 = HexToInt((char)buffer[pos + 2]);
                        int h2 = HexToInt((char)buffer[pos + 3]);
                        int h3 = HexToInt((char)buffer[pos + 4]);
                        int h4 = HexToInt((char)buffer[pos + 5]);

                        if (h1 >= 0 && h2 >= 0 && h3 >= 0 && h4 >= 0)
                        {   // valid 4 hex chars
                            char ch = (char)((h1 << 12) | (h2 << 8) | (h3 << 4) | h4);
                            i += 5;

                            b = (byte)((h1 << 4) | h2);
                            buffer[count++] = b;
                            b = (byte)((h3 << 4) | h4);
                            buffer[count++] = b;
                            continue;
                        }
                    }
                    else
                    {
                        int h1 = HexToInt((char)buffer[pos + 1]);
                        int h2 = HexToInt((char)buffer[pos + 2]);

                        if (h1 >= 0 && h2 >= 0)
                        {   // valid 2 hex chars
                            b = (byte)((h1 << 4) | h2);
                            i += 2;
                        }
                    }
                }

                buffer[start+count++] = b;
            }
            return new AString(buffer, start, count);
        }
    }
}
