using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Affecto.ActiveDirectoryService
{
    internal class CachedActiveDirectoryService : ActiveDirectoryService
    {
        private const string CacheName = "Affecto.ActiveDirectoryService";
        private static readonly MemoryCache Cache = new MemoryCache(CacheName);
        private static readonly object CacheUpdateLock = new object();

        private readonly TimeSpan cacheDuration;

        public CachedActiveDirectoryService(string domainPath, TimeSpan cacheDuration)
            : base(domainPath)
        {
            this.cacheDuration = cacheDuration;
        }

        public override IPrincipal GetUser(string userName)
        {
            var cacheKey = CreateCacheKey("GetUser", userName);
            return GetCachedObject(cacheKey, () => base.GetUser(userName));
        }

        public override IEnumerable<IPrincipal> GetGroupMemberPrincipals(string groupName, bool recursive)
        {
            var cacheKey = CreateCacheKey("GetGroupMemberPrincipals", groupName, recursive.ToString());
            return GetCachedObject(cacheKey, () => base.GetGroupMemberPrincipals(groupName, recursive));
        }

        protected override IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            var cacheKey = CreateCacheKey("GetGroupMembers", groupName);
            return GetCachedObject(cacheKey, () => base.GetGroupMemberAccountNames(groupName));
        }

        protected override IEnumerable<IPrincipal> ResolveMembers(Principal parent, bool isRecursive)
        {
            var cacheKey = CreateCacheKey("ResolveMembers", parent.DomainPath, isRecursive.ToString());
            return GetCachedObject(cacheKey, () => base.ResolveMembers(parent, isRecursive));
        }

        private string CreateCacheKey(string id, params string[] keys)
        {
            return string.Format("{0}_{1}", id, string.Join("_", keys));
        }

        private T GetCachedObject<T>(string key, Func<T> resolver) where T : class
        {
            T userOrNull = Cache.Get(key) as T;
            if (userOrNull != null)
            {
                return userOrNull;
            }

            lock (CacheUpdateLock)
            {
                userOrNull = Cache.Get(key) as T;
                if (userOrNull != null)
                {
                    return userOrNull;
                }

                T item = resolver.Invoke();
                if (item != null)
                {
                    Cache.Add(key, item, new DateTimeOffset(DateTime.UtcNow.Add(cacheDuration)));
                }
                return item;
            }
        }
    }
}