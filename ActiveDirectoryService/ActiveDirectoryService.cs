using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class ActiveDirectoryService : IActiveDirectoryService
    {
        private readonly string domainPath;

        public ActiveDirectoryService(string domainPath)
        {
            if (string.IsNullOrWhiteSpace(domainPath))
            {
                throw new ArgumentException("domainPath must be defined.");
            }
            this.domainPath = domainPath;
        }

        public virtual IPrincipal GetUser(string userName)
        {
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainEntry, userName))
            {
                return searcher.Find();
            }
        }

        public bool IsGroupMember(string userName, string groupName)
        {
            return !string.IsNullOrWhiteSpace(userName)
                && GetGroupMemberAccountNames(groupName).Any(o => o.Equals(userName, StringComparison.OrdinalIgnoreCase));
        }

        public virtual IEnumerable<IPrincipal> GetGroupMemberPrincipals(string groupName, bool recursive)
        {
            List<IPrincipal> result = new List<IPrincipal>();
            using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath))
            {
                using (PrincipalSearcher principalSearcher = new PrincipalSearcher(domainEntry, groupName))
                {
                    Principal groupPrincipal = principalSearcher.Find();
                    result.AddRange(ResolveMembers(groupPrincipal, recursive));
                }
            }
            return result;
        }

        protected virtual IEnumerable<string> GetGroupMemberAccountNames(string groupName)
        {
            using (var ctx = new PrincipalContext(ContextType.Domain, domainPath))
            using (var group = GroupPrincipal.FindByIdentity(ctx, groupName))
            {
                if (group != null)
                {
                    return group.GetMembers(true).Select(o => o.SamAccountName).ToList();
                }
            }
            return Enumerable.Empty<string>();
        }

        protected virtual IEnumerable<IPrincipal> ResolveMembers(Principal parent, bool isRecursive)
        {
            if (!parent.IsGroup)
            {
                return Enumerable.Empty<IPrincipal>();
            }

            var result = new List<IPrincipal>();

            if (parent.MemberValueCollection != null)
            {
                IEnumerator memberEnumerator = parent.MemberValueCollection.GetEnumerator();

                while (memberEnumerator.MoveNext())
                {
                    if (memberEnumerator.Current != null)
                    {
                        string userDomainPath = AdDomainPathHandler.Escape(memberEnumerator.Current.ToString());

                        if (!result.Any(e => e.DomainPath.Equals(domainPath + "/" + userDomainPath)))
                        {
                            using (var childDirectoryEntry = new DirectoryEntry(domainPath + "/" + userDomainPath))
                            {
                                Principal childPrincipal = new Principal(childDirectoryEntry);
                                result.Add(childPrincipal);
                                if (isRecursive)
                                {
                                    IEnumerable<IPrincipal> childPrincipalMembers = ResolveMembers(childPrincipal, true);
                                    result.AddRange(childPrincipalMembers.Where(p => !result.Any(r => r.NativeGuid == p.NativeGuid)));
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}