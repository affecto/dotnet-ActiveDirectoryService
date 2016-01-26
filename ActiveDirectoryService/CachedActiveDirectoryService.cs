using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Affecto.ActiveDirectoryService
{
    internal class CachedActiveDirectoryService : ActiveDirectoryService
    {
        private const string CacheName = "Affecto.ActiveDirectoryService";
        private const string GetUserKey = "GetUserKey";
        private const string GetGroupMembersKey = "GetGroupMembers";
        private const string GetGroupMemberAccountNamesKey = "GetGroupMemberAccountNames";
        private const string ResolveMembersKey = "ResolveMembers";

        private static readonly MemoryCache Cache = new MemoryCache(CacheName);
        private static readonly Dictionary<string, object> Locks = new Dictionary<string, object>
        {
            { GetUserKey, new object() },
            { GetGroupMembersKey, new object() },
            { GetGroupMemberAccountNamesKey, new object() },
            { ResolveMembersKey, new object() }
        };

        private readonly TimeSpan cacheDuration;

        public CachedActiveDirectoryService(DomainPath domainPath, TimeSpan cacheDuration)
            : base(domainPath)
        {
            this.cacheDuration = cacheDuration;
        }

        public override IPrincipal GetUser(string userName, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetUserKey, userName, FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetUserKey, cacheKey, () => base.GetUser(userName, additionalPropertyNames));
        }

        public override IEnumerable<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetGroupMembersKey, groupName, recursive.ToString(), FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetGroupMembersKey, cacheKey, () => base.GetGroupMembers(groupName, recursive, additionalPropertyNames));
        }

        protected override IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            string cacheKey = CreateCacheKey(GetGroupMemberAccountNamesKey, groupName);
            return GetCachedValue(GetGroupMemberAccountNamesKey, cacheKey, () => base.GetGroupMemberAccountNames(groupName));
        }

        protected override IEnumerable<IPrincipal> ResolveMembers(Principal parent, bool isRecursive, ICollection<string> additionalPropertyNames)
        {
            string cacheKey = CreateCacheKey(ResolveMembersKey, parent.DomainPath, isRecursive.ToString(), FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(ResolveMembersKey, cacheKey, () => base.ResolveMembers(parent, isRecursive, additionalPropertyNames));
        }

        private static string CreateCacheKey(string id, params string[] keys)
        {
            return string.Format("{0}_{1}", id, string.Join("_", keys));
        }

        private static string FormatAdditionalPropertyNames(ICollection<string> additionalPropertyNames)
        {
            if (additionalPropertyNames == null)
            {
                return string.Empty;
            }

            return string.Join("-", additionalPropertyNames);
        }

        private T GetCachedValue<T>(string lockName, string key, Func<T> resolver) where T : class
        {
            T value = Cache.Get(key) as T;
            if (value != null)
            {
                return value;
            }

            lock (Locks[lockName])
            {
                value = Cache.Get(key) as T;
                if (value != null)
                {
                    return value;
                }

                value = resolver.Invoke();
                if (value != null)
                {
                    Cache.Add(key, value, new DateTimeOffset(DateTime.UtcNow.Add(cacheDuration)));
                }

                return value;
            }
        }
    }
}