using Microsoft.AspNetCore.Mvc;
using ZeroGallery.Shared.Models;

namespace ZeroGalleryApp.Controllers
{
    public abstract class BaseController
        : Controller
    {
        public BaseController()
        {
        }

        /*protected async Task SaveToDbCache(string key, byte[] value, TimeSpan expirationPeriod)
        {
            var record = new DBCacheRecord(key) { Value = value, Timestamp = Timestamp.UtcNow, ExpirationTimestamp = Timestamp.UtcNowAddTimeSpan(expirationPeriod) };
            await Tables.Cache.Write(record);
        }

        protected async Task<T> GetFromDbCache<T>(string key)
            where T : IBinarySerializable
        {
            var record = (await Tables.Cache.Get(Builders<DBCacheRecord>.Filter.Eq(a => a.Key, key)))?.FirstOrDefault();
            if (record != null && record.Value != null)
            {
                var o = MessageSerializer.Deserialize<T>(record.Value);
                return o;
            }
            return default(T)!;
        }*/

        protected OperationContext OperationContext
        {
            get
            {
                return (HttpContext.Items["op_context"] as OperationContext)!;
            }
        }
    }
}
