using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Affecto.ActiveDirectoryService
{
    internal class ActiveDirectoryService : IActiveDirectoryService
    {
        private const string LdapPathPrefix = "LDAP://";

        private readonly DomainPath domainPath;
        private readonly string defaultDomain;
        private readonly PriorityList domainPathList = new PriorityList();

        public ActiveDirectoryService(DomainPath domainPath)
        {
            if (domainPath == null)
            {
                throw new ArgumentNullException("domainPath", "Domain path must be defined.");
            }
            this.domainPath = domainPath;
            defaultDomain = GetDefaultDomain();
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
                SecurityIdentifier sid = GetObjectSid(domainEntry);
                UserPrincipal principal = Principal.FromDirectoryEntry<UserPrincipal>(domainPath, domainEntry);
                IReadOnlyCollection<IPrincipal> parentGroups = GetGroupsWhereUserIsMemberInternal(principal);
                IReadOnlyCollection<IPrincipal> foreignGroups = GetGroupsWhereUserIsMemberInternal(sid);

                return parentGroups.Concat(foreignGroups.Where(o => parentGroups.All(p => p.NativeGuid != o.NativeGuid))).ToList();
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

        protected virtual IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMemberInternal(SecurityIdentifier sid)
        {
            List<IPrincipal> resolvedGroups = new List<IPrincipal>();

            GetItem<object>(path =>
            {
                string namingContext = GetNamingContext(path);
                string filter = string.Format("(&(member=CN={0},CN=ForeignSecurityPrincipals,{1}))", sid.Value, namingContext);

                using (DirectoryEntry entry = new DirectoryEntry(path))
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = filter;
                    searcher.PropertiesToLoad.Add(ActiveDirectoryProperties.DistinguishedName);
                    SearchResultCollection searchResults = searcher.FindAll();

                    foreach (SearchResult result in searchResults)
                    {
                        List<IPrincipal> parents = GetParentDomainPaths(GetPathWithProtocol(result.Path), result.Properties[ActiveDirectoryProperties.DistinguishedName][0].ToString());
                        resolvedGroups.AddRange(parents.Where(o => resolvedGroups.All(p => p.NativeGuid != o.NativeGuid)));
                    }
                }
                return null;
            }, false);

            return resolvedGroups;
        }

        protected virtual List<IPrincipal> GetParentDomainPaths(string path, string distinguishedName)
        {
            List<IPrincipal> principals = new List<IPrincipal>();
            string fullDomainPath = path + "/" + distinguishedName;
            using (DirectoryEntry entry = new DirectoryEntry(fullDomainPath))
            {
                GroupPrincipal principal = Principal.FromDirectoryEntry(new DomainPath(path), entry) as GroupPrincipal;
                if (principal != null)
                {
                    principals.Add(principal);
                    foreach (string memberOf in entry.Properties[ActiveDirectoryProperties.MemberOf])
                    {
                        List<IPrincipal> groups = GetParentDomainPaths(path, memberOf);
                        principals.AddRange(groups.Where(o => principals.All(p => p.NativeGuid != o.NativeGuid)));
                    }
                }
            }

            return principals;
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
            List<IPrincipal> results = new List<IPrincipal>();
            List<SecurityIdentifier> securityIdentifiers = new List<SecurityIdentifier>();

            string parentDomainPath = GetPathWithProtocol(parent.DomainPath);
            foreach (string childDomainPath in parent.ChildDomainPaths)
            {
                string fullChildDomainPath = string.Format("{0}/{1}", parentDomainPath, childDomainPath);
                using (DirectoryEntry childDirectoryEntry = new DirectoryEntry(fullChildDomainPath))
                {
                    securityIdentifiers.Add(GetObjectSid(childDirectoryEntry));
                }
            }

            foreach (SecurityIdentifier securityIdentifier in securityIdentifiers)
            {
                IPrincipal childPrincipal = GetPrincipalInternal(securityIdentifier, additionalPropertyNames);
                if (childPrincipal != null)
                {
                    results.Add(childPrincipal);
                    if (isRecursive && childPrincipal.IsGroup)
                    {
                        IEnumerable<IPrincipal> childPrincipalMembers = ResolveMembers((GroupPrincipal)childPrincipal, true, additionalPropertyNames);
                        results.AddRange(childPrincipalMembers.Where(p => results.All(r => r.NativeGuid != p.NativeGuid)));
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
            string domain = GetDomain(accountName);
            string path = GetDomainPath(domain).GetPathWithProtocol();
            using (DirectoryEntry domainEntry = new DirectoryEntry(path))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipal<T>(accountName);
            }
        }

        protected DirectoryEntry GetDirectoryEntryByNativeGuid(Guid nativeGuid)
        {
            return GetItem(path =>
            {
                const string guidFilterFormat = "{0}/<GUID={1}>";
                string pathWithGuid = string.Format(guidFilterFormat, path, nativeGuid.ToString("N"));
                return DirectoryEntry.Exists(pathWithGuid) ? new DirectoryEntry(pathWithGuid) : null;
            });
        }

        protected virtual IPrincipal GetPrincipalInternal(SecurityIdentifier sid, ICollection<string> additionalPropertyNames = null)
        {
            return GetItem(path =>
            {
                using (DirectoryEntry entry = SearchMember(path, sid))
                {
                    if (entry != null)
                    {
                        return Principal.FromDirectoryEntry(new DomainPath(path), entry, additionalPropertyNames);
                    }

                    return null;
                }
            });
        }

        protected virtual string GetNamingContext(string path)
        {
            using (DirectoryEntry rootDirEntry = new DirectoryEntry(string.Format("{0}/RootDSE", path)))
            {
                return (string)rootDirEntry.Properties["defaultNamingContext"].Value;
            }
        }

        private T GetItem<T>(Func<string, T> resolver, bool useDemoted = true) where T : class
        {
            foreach (string path in GetAllDomainsWithProtocol(useDemoted))
            {
                try
                {
                    var value = resolver.Invoke(path);
                    if (value != null)
                    {
                        domainPathList.Promote(path);
                        return value;
                    }
                }
                catch (COMException)
                {
                    domainPathList.Demote(path);
                }
            }
            return null; // Todo: Not found exception
        }

        private DomainPath GetDomainPath(string domain = null)
        {
            if (string.IsNullOrEmpty(domain) || defaultDomain.Equals(domain, StringComparison.CurrentCultureIgnoreCase))
            {
                return domainPath;
            }

            return ResolveDomainPath(domain);
        }

        private string GetDefaultDomain()
        {
            using (DirectoryEntry entry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
            {
                return entry.Properties["dc"].Value.ToString();
            }
        }

        private string GetDomain(string accountName)
        {
            return accountName.Contains('\\') ? accountName.Split('\\')[0] : null;
        }

        private DomainPath ResolveDomainPath(string domain)
        {
            string filter = string.Format("(flatName={0})", domain);
            string foreignDomain = ResolveForeignDomains(domainPath.GetPathWithProtocol(), filter, "name").First();
            return new DomainPath(foreignDomain);
        }

        private IEnumerable<string> GetAllDomainsWithProtocol(bool includeDemoted = true)
        {
            string defaultDomainPath = domainPath.GetPathWithProtocol();
            PriorityList clone = domainPathList.Clone();

            // Yield paths which have been verified to contain valid principals
            foreach (string path in clone.GetPromoted())
            {
                yield return path;
            }

            // Yield default domain path
            if (!clone.ContainsKey(defaultDomainPath))
            {
                clone.Promote(defaultDomainPath);
                yield return defaultDomainPath;
            }

            // Resolve path from AD and yield new
            string filter = "(objectClass=trustedDomain)";

            List<string> domains = ResolveForeignDomains(defaultDomainPath, filter, "name").ToList();
            foreach (string path in domains.Select(o => new DomainPath(o).GetPathWithProtocol()))
            {
                if (!clone.ContainsKey(path))
                {
                    clone.Promote(path);
                    yield return path;
                }
            }

            // Yield the rest of the paths
            if (includeDemoted)
            {
                foreach (string path in clone.GetDemoted())
                {
                    yield return path;
                }
            }
        }

        private IEnumerable<string> ResolveForeignDomains(string path, string filter, string property)
        {
            string distinguishedName = GetNamingContext(path);

            using (DirectoryEntry entry = new DirectoryEntry(string.Format("{0}/cn=system,{1}", path, distinguishedName)))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = filter;
                searcher.PropertiesToLoad.Add(property);
                SearchResultCollection results = searcher.FindAll();

                foreach (SearchResult result in results)
                {
                    yield return result.Properties[property][0].ToString();
                }
            }
        }

        private static DirectoryEntry SearchMember(string path, SecurityIdentifier sid)
        {
            string filter = string.Format("(&(objectSid={0})(!objectClass=foreignSecurityPrincipal))", sid);

            using (DirectoryEntry entry = new DirectoryEntry(path))
            using (DirectorySearcher searcher = new DirectorySearcher(entry))
            {
                searcher.Filter = filter;
                SearchResult searchResult = searcher.FindOne();
                if (searchResult != null)
                {
                    return searchResult.GetDirectoryEntry();
                }
            }
            return null; // Todo: Exception?
        }

        private static SecurityIdentifier GetObjectSid(DirectoryEntry domainEntry)
        {
            byte[] sid = (byte[])domainEntry.Properties[ActiveDirectoryProperties.ObjectSid].Value;
            return new SecurityIdentifier(sid, 0);
        }

        private static string GetPathWithProtocol(string parent)
        {
            string parentDomain;
            if (parent.StartsWith(LdapPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                parentDomain = parent.Substring(LdapPathPrefix.Length).Split('/')[0];
            }
            else
            {
                parentDomain = parent.Split('/')[0];
            }

            return LdapPathPrefix + parentDomain;
        }
    }
}