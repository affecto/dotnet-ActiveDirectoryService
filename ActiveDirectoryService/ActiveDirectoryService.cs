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

        public virtual IPrincipal GetUser(string userName, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipal(userName);
            }
        }

        public bool IsGroupMember(string userName, string groupName)
        {
            return !string.IsNullOrWhiteSpace(userName)
                && GetGroupMemberAccountNames(groupName).Any(o => o.Equals(userName, StringComparison.OrdinalIgnoreCase));
        }

        public virtual IEnumerable<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null)
        {
            List<IPrincipal> result = new List<IPrincipal>();
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            {
                using (PrincipalSearcher principalSearcher = new PrincipalSearcher(domainEntry))
                {
                    Principal groupPrincipal = principalSearcher.FindPrincipal(groupName);
                    result.AddRange(ResolveMembers(groupPrincipal, recursive, additionalPropertyNames));
                }
            }
            return result;
        }

        public virtual IEnumerable<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipals(ldapFilter);
            }
        }

        protected virtual IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            using (var ctx = new PrincipalContext(ContextType.Domain, domainPath.GetPathWithoutProtocol()))
            using (var group = GroupPrincipal.FindByIdentity(ctx, groupName))
            {
                if (group != null)
                {
                    return group.GetMembers(true).Select(o => o.SamAccountName).ToList();
                }
            }
            return Enumerable.Empty<string>();
        }

        protected virtual IEnumerable<IPrincipal> ResolveMembers(Principal parent, bool isRecursive, ICollection<string> additionalPropertyNames)
        {
            if (!parent.IsGroup)
            {
                return Enumerable.Empty<IPrincipal>();
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
    }
}