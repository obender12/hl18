using System.Threading;

namespace hl18
{
    public partial class Storage
    {
        private SpinLock updateLock = new SpinLock(false);

        // options
        public int Now;
        public int IsRatingRun;

        public bool bufferFromEmail(AString email, out byte[] buffer)
        {
            var amp = email.IndexOf((byte)'@');
            if (amp < 0 || amp == email.Length - 1)
            {
                buffer = null;
                return false;
            }
            byte domainIdx = (byte)Domains.GetOrCreateRange(email.Substring(amp + 1)).Index;
            buffer = new byte[amp + 1];
            for (int i = 0; i < amp; i++)
                buffer[i] = email[i];
            buffer[amp] = domainIdx;
            return true;
        }

        public AStringBuilder emailFromBuffer(byte[] buffer, AStringBuilder sb)
        {
            var domain = Domains[buffer[buffer.Length - 1]].Name;
            for (int i = 0; i < buffer.Length - 1; i++)
                sb.Append((char)(buffer[i]));
            sb.Append('@');
            sb.Append(domain);
            return sb;
        }

        public AString areaCodeFromPhone(AString phone)
        {
            if (phone.IsEmpty)
                return AString.Empty;
            var openBrace = phone.IndexOf('(');
            var closeBrace = phone.IndexOf(')');
            if( openBrace>=0 && closeBrace==openBrace+4 )
                return phone.Substring(openBrace + 1, 3);
            return null;
        }
    }
}
