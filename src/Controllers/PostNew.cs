using Microsoft.AspNetCore.Http;
using System;
using System.Text;
using System.Threading.Tasks;
using Utf8Json;

namespace hl18
{
    // POST /accounts/new/
    public class PostNew: ICtxProcessor
    {
        private readonly Storage store;
        public PostNew(Storage storage)
        {
            store = storage;
        }

        // synchronously process the request from requestBuffer, and return statusCode
        public int Process(HttpCtx ctx, int dummy)
        {
            var startTime = Stats.Watch.Elapsed;

            // path sanity check
            foreach (var query in ctx.Params)
            {
                var value = query.Value;
                if (value.IsEmpty)
                    return 400;
                if (query.Key != "query_id")
                    // all other parameters are invalid
                    return 400;
            }

            // load the body
            if (ctx.RequestBodyStart==ctx.ResponseBodyStart)
                return 400;

            // parse Json and process dto
            var dto = DtoAccount.Obtain();
            int statusCode = 400;
            try
            {
                JsonReader reader = new JsonReader(ctx.Buffer, ctx.RequestBodyStart);
                if (DtoAccount.Parse(ref reader, dto, store) && dto.flags != 0)
                {
                    // add the new account
                    statusCode = store.ValidateNewAccount(dto);
                    if (statusCode == 201)
                        ctx.PostAction = () =>
                        {
                            store.PostNewAccount(dto);
                            DtoAccount.Release(dto);
                        };
                }
            }
            catch (Exception )
            {
                // fall through
            }

            // return the borrowed object
            if( ctx.PostAction==null )
                DtoAccount.Release(dto);

            var stopTime = Stats.Watch.Elapsed;
            ctx.ContextType = "PostNew";
            return statusCode;
        }

    }
}
