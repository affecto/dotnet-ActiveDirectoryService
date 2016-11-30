using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly DomainPath domainPath;

        public ActiveDirectoryService(DomainPath domainPath)
        {
            if (domainPath == null)
            {
                throw new ArgumentNullException("domainPath", "Domain path must be defined.");
            }
            this.domainPath = domainPath;
        }

        public IPrincipal GetPrincipal(string accountName, ICollection<string> additionalPropertyNames = null)
        {
            return GetPrincipalInternal<Principal>(accountName, additionalPropertyNames);
        }

        public IPrincipal GetPrincipal(Guid nativeGuid, ICollection<string> additionalPropertyNames = null)
        {
            return GetPrincipalInternal<Principal>(nativeGuid, additionalPropertyNames);
        }

        public bool IsGroupMember(string accountName, string groupName)
        {
            return !string.IsNullOrWhiteSpace(accountName)
                && GetGroupMemberAccountNames(groupName).Any(member => member.Equals(accountName, StringComparison.OrdinalIgnoreCase));
        }

        public virtual IReadOnlyCollection<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            List<IPrincipal> result = new List<IPrincipal>();

            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher principalSearcher = new PrincipalSearcher(domainPath, domainEntry))
            {
                GroupPrincipal groupPrincipal = principalSearcher.FindPrincipal<GroupPrincipal>(groupName);
                result.AddRange(ResolveMembers(groupPrincipal, recursive, additionalPropertyNames));
            }

            return result;
        }

        public virtual IReadOnlyCollection<IPrincipal> GetGroupMembers(Guid nativeGuid, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            GroupPrincipal principal = GetPrincipalInternal<GroupPrincipal>(nativeGuid);
            return ResolveMembers(principal, recursive, additionalPropertyNames);
        }

        public virtual IReadOnlyCollection<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipals<Principal>(ldapFilter);
            }
        }

        public virtual IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMember(Guid userNativeGuid)
        {
            using (DirectoryEntry domainEntry = GetDirectoryEntryByNativeGuid(userNativeGuid))
            {
                UserPrincipal principal = Principal.FromDirectoryEntry<UserPrincipal>(domainPath, domainEntry);
                return GetGroupsWhereUserIsMemberInternal(principal);
            }
        }

        public IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMember(string userAccountName)
        {
            UserPrincipal principal = GetPrincipalInternal<UserPrincipal>(userAccountName);
            return GetGroupsWhereUserIsMemberInternal(principal);

        }

        protected virtual IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMemberInternal(UserPrincipal principal)
        {
            var groups = new List<IPrincipal>();

            foreach (string parentDomainPath in principal.ParentDomainPaths)
            {
                using (var entry = new DirectoryEntry(new DomainPath(parentDomainPath).GetPathWithProtocol()))
                {
                    groups.Add(Principal.FromDirectoryEntry(domainPath, entry));
                }
            }

            if (!groups.Select(p => p.AccountName).Any(accountName => accountName.ToLower().Contains("domain users")))
            {
                groups.Add(GetPrincipalInternal<GroupPrincipal>("Domain users"));
            }

            return groups;
        }

        protected virtual IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            using (var context = new PrincipalContext(ContextType.Domain, domainPath.GetPathWithoutProtocol()))
            using (var group = System.DirectoryServices.AccountManagement.GroupPrincipal.FindByIdentity(context, groupName))
            {
                if (group != null)
                {
                    return group.GetMembers(true).Select(member => member.SamAccountName).ToList();
                }
            }

            return Enumerable.Empty<string>();
        }

        protected virtual IReadOnlyCollection<IPrincipal> ResolveMembers(GroupPrincipal parent, bool isRecursive, ICollection<string> additionalPropertyNames)
        {
            if (!parent.IsGroup)
            {
                return new IPrincipal[0];
            }

            var results = new List<IPrincipal>();

            foreach (string childDomainPath in parent.ChildDomainPaths)
            {
                string fullChildDomainPath = domainPath.GetPathWithProtocol() + "/" + childDomainPath;
                if (!results.Any(user => user.DomainPath.Equals(fullChildDomainPath)))
                {
                    using (var childDirectoryEntry = new DirectoryEntry(fullChildDomainPath))
                    {
                        Principal childPrincipal = Principal.FromDirectoryEntry(domainPath, childDirectoryEntry, additionalPropertyNames);
                        results.Add(childPrincipal);
                        if (isRecursive && childPrincipal.IsGroup)
                        {
                            IEnumerable<IPrincipal> childPrincipalMembers = ResolveMembers((GroupPrincipal) childPrincipal, true, additionalPropertyNames);
                            results.AddRange(childPrincipalMembers.Where(p => results.All(r => r.NativeGuid != p.NativeGuid)));
                        }
                    }
                }
            }

            return results;
        }

        protected virtual T GetPrincipalInternal<T>(Guid nativeGuid, ICollection<string> additionalPropertyNames = null) where T : Principal
        {
            using (DirectoryEntry domainEntry = GetDirectoryEntryByNativeGuid(nativeGuid))
            {
                return Principal.FromDirectoryEntry<T>(domainPath, domainEntry, additionalPropertyNames);
            }
        }

        protected virtual T GetPrincipalInternal<T>(string accountName, ICollection<string> additionalPropertyNames = null) where T : Principal
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipal<T>(accountName);
            }
        }

        protected DirectoryEntry GetDirectoryEntryByNativeGuid(Guid nativeGuid)
        {
            const string guidFilterFormat = "{0}/<GUID={1}>";
            string path = string.Format(guidFilterFormat, domainPath.GetPathWithProtocol(), nativeGuid.ToString("N"));

            return new DirectoryEntry(path);
        }
    }
}