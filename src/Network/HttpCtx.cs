using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace hl18
{
    public class HttpCtx 
    {
        public static int MAX_BUFFER = 16384;

        // constructor: create a new buffer
        public HttpCtx()
        {
            Buffer = new byte[MAX_BUFFER];
        }

        // constructor: take external buffer
        public HttpCtx(byte[] buffer)
        {
            Buffer = buffer;
        }

        public byte[] Buffer;

        // request
        public int RequestBodyStart;  // parsed by IO thread
        public int RequestBodyLength; // parsed by IO thread
        public int RequestLength => RequestBodyStart + RequestBodyLength;

        // parsed headers
        public AString FirstLine; // first line of the headers
        public AString Method; // GET or POST
        public AString Path; // /accounts/filter/
        public QueryParams Params = new QueryParams(16);
        public int QueryId; // for tracing

        public int ResponseStart => 8192;
        public int ResponseBodyStart; // filled by the processor
        public int ResponseBodyLength; // filled by the processor
        public int ResponseTotalLength => ResponseBodyStart + ResponseBodyLength - ResponseStart;
        public Action PostAction; // called after sending the response, useful for POST requests

        //private static readonly AString crlf = new AString(new byte[] { 13, 10 });
        private static readonly AString content_length = new AString("Content-Length");

        // called by socket reader to find the total read size
        public unsafe int ParseRequestHeader(int totalRead)
        {
            if (RequestBodyStart == 0)
                RequestBodyStart = findHeaderEnd(totalRead);
            if( RequestBodyStart>0 )
            {
                if (Method.IsEmpty) // headers unparsed
                {
                    var headers = new AString(Buffer, 0, RequestBodyStart);
                    while( headers.Length>0 )
                    {
                        // get the next line
                        var i = headers.IndexOfCRLF();
                        if (i < 0)
                            break;

                        // get the next line and advance the header span
                        var line = headers.Substring(0, i);
                        headers = headers.Substring(i + 2);

                        // remove leading spaces
                        while (line.Length>0 && line[0] < 'A')
                            line = line.Substring(1);
                        
                        // skip the empty line
                        if (line.Length == 0)
                            continue; 

                        if ( Method.IsEmpty ) // first line is not parsed, so this is it
                        {
                            FirstLine = line;
                            ParseFirstLine(line);
                        }
                        else 
                        // the only other line we are interested in is content-length
                        if(RequestBodyLength == 0 )
                        {
                            //var str = Encoding.ASCII.GetString(line); // for debugging, remove
                            if ( line.StartsWith(content_length) )
                            {
                                i = line.IndexOf((byte)':');
                                line = line.Substring(i + 2);
                                line.TryToInt(out RequestBodyLength);
                            }
                        }
                    }

                }
                return ResponseStart;
            }
            return 0;
        }

        public void ParseFirstLine(AString firstLine)
        {
            var parts = firstLine.Split(' ');
            Trace.Assert(parts.Length == 3);
            Method = parts[0];
            var pathParts = parts[1].Split('?');
            Path = pathParts[0];
            if (pathParts.Length == 1 || pathParts[1].IsEmpty)
                Params.Clear();
            else
                ParseParams(pathParts[1]);
        }


        // string, not AString, as parameters can be non-ASCII
        public void ParseParams(AString paramStr)
        {
            Params.Clear();
            paramStr = paramStr.InPlaceUrlDecode();
            var paramParts = paramStr.Split('&');
            for (int i = 0; i < paramParts.Length; i++)
            {
                var pp = paramParts[i].Split('=');
                Params.Add(pp[0].ToString(), pp[1].ToString());
                if (pp[0] == "query_id")
                    pp[1].TryToInt(out QueryId);
            }
        }

        private unsafe int findHeaderEnd(int read)
        {
            if (read <= 4)
                return 0;
            for (int i = 4; i <= read; i++)
                if (Buffer[i - 1] == 10 && Buffer[i - 2] == 13 && Buffer[i - 3] == 10 && Buffer[i - 4] == 13)
                    return i;
            return 0;
        }

        // response
        public int StatusCode;
        public string ContextType = string.Empty; // for stats
        public TaskCompletionSource<int> tcs; // todo: move it to HttpCtxWithTask derived class
        public object Tag; // general tag

        // reset
        public void Reset()
        {
            Method = AString.Empty;
            Path = AString.Empty;
            Params.Clear();
            QueryId = 0;
            RequestBodyStart = 0;
            RequestBodyLength = 0;
            //ResponseBodyStart = 0;
            //ResponseBodyLength = 0;
            StatusCode = 0;
            tcs = null;
            Tag = null;
        }

        private static ConcurrentBag<HttpCtx> bag = new ConcurrentBag<HttpCtx>();
        public static HttpCtx Obtain()
        {
            if (!bag.TryTake(out var obj))
                obj = new HttpCtx();
            return obj;
        }

        public static void Release(HttpCtx bb)
        {
            bb.Reset();
            bag.Add(bb);
        }

    }

}
