using System.Text;

namespace hl18
{
    public class AStringBuilder
    {
        private byte[] buffer;
        private int start;
        private int count;

        public int Count { get => count; }

        public AStringBuilder(byte[] buffer, int start)
        {
            this.buffer = buffer;
            this.start = start;
            count = 0;
        }

        public AStringBuilder Append(char c)
        {
            buffer[start + count++] = (byte)c;
            return this;
        }

        public AStringBuilder Append(AString astr)
        {
            for( int i=0; i<astr.Length; i++)
                buffer[start + count++] = astr[i];
            return this;
        }

        public AStringBuilder Append(byte[] buf)
        {
            for (int i = 0; i < buf.Length; i++)
                buffer[start + count++] = buf[i];
            return this;
        }
        /*
        public AStringBuilder Append(string str)
        {
            for (int i = 0; i < str.Length; i++)
                buffer[start + count++] = (byte)str[i];
            return this;
        }

        public AStringBuilder AppendUTF8(string str)
        {
            count += Encoding.UTF8.GetBytes(
                str, 0, str.Length, buffer, start + count);
            return this;
        }*/

        public unsafe AStringBuilder Append(int value)
        {
            count += ComposeInt(value, buffer, start + count);
            return this;
        }

        // non-negative
        public static unsafe int ComposeInt(int value, byte[] buffer, int start)
        {
            if (value == 0)
            {
                buffer[start] = (byte)'0';
                return 1;
            }

            var count = 0;
            if (value < 0)
            {
                value = -value;
                buffer[start] = (byte)'-';
                count = 1;
            }

            // compose one digit at a time
            byte* outbuf = stackalloc byte[30];
            int i = 0;
            while (value > 0)
            {
                outbuf[i++] = (byte)((value % 10) + 48);
                value /= 10;
            }

            // apply in the reverse order
            for (--i; i >= 0; i--)
                buffer[start + count++] = outbuf[i];

            return count;
        }
    }
}
