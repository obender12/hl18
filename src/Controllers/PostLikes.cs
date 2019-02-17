using System;

namespace hl18
{
    // POST /accounts/likes/
    public class PostLikes: ICtxProcessor
    {
        private readonly Storage store;
        public PostLikes(Storage storage)
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

            // parse Json and process dto
            var buffer = ctx.Buffer;
            var start = ctx.RequestBodyStart;
            var dto = DtoLikes.Obtain();

            int statusCode = 400;
            try
            {
                if (DtoLikes.Parse(buffer, start, dto, store))
                {
                    // update the likes in the storage
                    statusCode = store.VerifyNewLikes(dto);
                    if (statusCode == 202)
                        ctx.PostAction = () =>
                        {
                            store.PostNewLikes(dto);
                            DtoLikes.Release(dto);
                        };
                }
            }
            catch (Exception)
            {
                // fall through
            }

            if( ctx.PostAction==null )
                DtoLikes.Release(dto);

            var stopTime = Stats.Watch.Elapsed;
            ctx.ContextType = "PostLikes";
            // return the result
            return statusCode;
        }

    }
}
