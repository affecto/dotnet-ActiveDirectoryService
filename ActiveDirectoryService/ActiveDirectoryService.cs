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

        public virtual IPrincipal GetPrincipal(string accountName, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipal(accountName);
            }
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
            using (PrincipalSearcher principalSearcher = new PrincipalSearcher(domainEntry))
            {
                Principal groupPrincipal = principalSearcher.FindPrincipal(groupName);
                result.AddRange(ResolveMembers(groupPrincipal, recursive, additionalPropertyNames));
            }

            return result;
        }

        public virtual IReadOnlyCollection<IPrincipal> GetGroupMembers(Guid nativeGuid, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            Principal principal = GetPrincipalInternal(nativeGuid);
            return ResolveMembers(principal, recursive, additionalPropertyNames);
        }

        public virtual IReadOnlyCollection<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipals(ldapFilter);
            }
        }

        public IPrincipal GetPrincipal(Guid nativeGuid, ICollection<string> additionalPropertyNames = null)
        {
            return GetPrincipalInternal(nativeGuid, additionalPropertyNames);
        }

        protected virtual IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            using (var context = new PrincipalContext(ContextType.Domain, domainPath.GetPathWithoutProtocol()))
            using (var group = GroupPrincipal.FindByIdentity(context, groupName))
            {
                if (group != null)
                {
                    return group.GetMembers(true).Select(member => member.SamAccountName).ToList();
                }
            }

            return Enumerable.Empty<string>();
        }

        protected virtual IReadOnlyCollection<IPrincipal> ResolveMembers(Principal parent, bool isRecursive, ICollection<string> additionalPropertyNames)
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
                        Principal childPrincipal = Principal.FromDirectoryEntry(childDirectoryEntry, additionalPropertyNames);
                        results.Add(childPrincipal);
                        if (isRecursive)
                        {
                            IEnumerable<IPrincipal> childPrincipalMembers = ResolveMembers(childPrincipal, true, additionalPropertyNames);
                            results.AddRange(childPrincipalMembers.Where(p => results.All(r => r.NativeGuid != p.NativeGuid)));
                        }
                    }
                }
            }

            return results;
        }

        protected virtual Principal GetPrincipalInternal(Guid nativeGuid, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = GetDirectoryEntryByNativeGuid(nativeGuid))
            {
                return Principal.FromDirectoryEntry(domainEntry, additionalPropertyNames);
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