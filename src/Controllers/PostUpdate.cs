using System;
using Utf8Json;

namespace hl18
{
    // POST /accounts/<id>/
    public class PostUpdate: ICtxProcessor
    {
        private readonly Storage store;
        public PostUpdate(Storage storage)
        {
            store = storage;
        }

        // synchronously process the request from requestBuffer, and return statusCode
        public int Process(HttpCtx ctx, int extId)
        {
            var startTime = Stats.Watch.Elapsed;
            
            // query sanity check
            foreach (var query in ctx.Params)
            {
                var value = query.Value;
                if (value.IsEmpty)
                    return 400;
                if (query.Key != "query_id")
                    // all other parameters are invalid
                    return 400;
            }

            // translate to internal id and check for account existance
            if (!Mapper.ExtIdToIntId(extId, out int id) || !store.All[id])
                return 404;

            // parse Json 
            var dto = DtoAccount.Obtain();
            dto.id = id;

            int statusCode = 400;
            try
            {
                JsonReader reader = new JsonReader(ctx.Buffer, ctx.RequestBodyStart);
                if (DtoAccount.Parse(ref reader, dto, store) && dto.flags != 0 )
                {
                    // update the account, register post-process action
                    statusCode = store.ValidateUpdateAccount(dto);
                    if (statusCode == 202)
                        ctx.PostAction = () =>
                        {
                            store.PostUpdateAccount(dto);
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
            ctx.ContextType = "PostUpdate";
            return statusCode;
        }


    }
}
