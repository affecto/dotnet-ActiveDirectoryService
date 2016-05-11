using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Affecto.ActiveDirectoryService
{
    internal class CachedActiveDirectoryService : ActiveDirectoryService
    {
        private const string CacheName = "Affecto.ActiveDirectoryService";
        private const string GetPrincipalByAccountNameKey = "GetPrincipalByAccountName";
        private const string GetPrincipalByNativeGuidKey = "GetPrincipalByNativeGuid";
        private const string GetGroupMembersByGroupNameKey = "GetGroupMembersByGroupName";
        private const string GetGroupMembersByNativeGuidKey = "GetGroupMembersByNativeGuid";
        private const string GetGroupMemberAccountNamesKey = "GetGroupMemberAccountNames";
        private const string SearchPrincipalsKey = "SearchPrincipals";
        private const string ResolveMembersKey = "ResolveMembers";

        private static readonly MemoryCache Cache = new MemoryCache(CacheName);
        private static readonly Dictionary<string, object> Locks = new Dictionary<string, object>
        {
            { GetPrincipalByAccountNameKey, new object() },
            { GetPrincipalByNativeGuidKey, new object() },
            { GetGroupMembersByGroupNameKey, new object() },
            { GetGroupMembersByNativeGuidKey, new object() },
            { GetGroupMemberAccountNamesKey, new object() },
            { SearchPrincipalsKey, new object() },
            { ResolveMembersKey, new object() }
        };

        private readonly TimeSpan cacheDuration;

        public CachedActiveDirectoryService(DomainPath domainPath, TimeSpan cacheDuration)
            : base(domainPath)
        {
            this.cacheDuration = cacheDuration;
        }

        public override IPrincipal GetPrincipal(string accountName, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetPrincipalByAccountNameKey, accountName, FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetPrincipalByAccountNameKey, cacheKey, () => base.GetPrincipal(accountName, additionalPropertyNames));
        }

        public override IReadOnlyCollection<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetGroupMembersByGroupNameKey, groupName, recursive.ToString(), FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetGroupMembersByGroupNameKey, cacheKey, () => base.GetGroupMembers(groupName, recursive, additionalPropertyNames));
        }

        public override IReadOnlyCollection<IPrincipal> GetGroupMembers(Guid nativeGuid, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetGroupMembersByNativeGuidKey, nativeGuid.ToString("N"), recursive.ToString(), FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetGroupMembersByNativeGuidKey, cacheKey, () => base.GetGroupMembers(nativeGuid, recursive, additionalPropertyNames));
        }

        public override IReadOnlyCollection<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(SearchPrincipalsKey, ldapFilter, FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(SearchPrincipalsKey, cacheKey, () => base.SearchPrincipals(ldapFilter, additionalPropertyNames));
        }

        protected override Principal GetPrincipalInternal(Guid nativeGuid, ICollection<string> additionalPropertyNames = null)
        {
            string cacheKey = CreateCacheKey(GetPrincipalByNativeGuidKey, nativeGuid.ToString("N"), FormatAdditionalPropertyNames(additionalPropertyNames));
            return GetCachedValue(GetPrincipalByNativeGuidKey, cacheKey, () => base.GetPrincipalInternal(nativeGuid, additionalPropertyNames));
        }

        protected override IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            string cacheKey = CreateCacheKey(GetGroupMemberAccountNamesKey, groupName);
            return GetCachedValue(GetGroupMemberAccountNamesKey, cacheKey, () => base.GetGroupMemberAccountNames(groupName));
        }

        protected override IReadOnlyCollection<IPrincipal> ResolveMembers(Principal parent, bool isRecursive, ICollection<string> additionalPropertyNames)
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