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
        private readonly DomainPath domainPath;
        private readonly Dictionary<string, int> prioritizedDomainPaths = new Dictionary<string, int>();

        public ActiveDirectoryService(DomainPath domainPath)
        {
            if (domainPath == null)
            {
                throw new ArgumentNullException("domainPath", "Domain path must be defined.");
            }
            this.domainPath = domainPath;
        }

        private string defaultDomain;
        private string DefaultDomain
        {
            get { return defaultDomain ?? (defaultDomain = GetDefaultDomain()); }
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
            var securityIdentifiers = new List<SecurityIdentifier>();

            foreach (string childDomainPath in parent.ChildDomainPaths)
            {
                string fullChildDomainPath = GetDomainPathWithProtocol() + "/" + childDomainPath;
                if (!results.Any(user => user.DomainPath.Equals(fullChildDomainPath)))
                {
                    using (var childDirectoryEntry = new DirectoryEntry(fullChildDomainPath))
                    {
                        byte[] objectSid = (byte[])childDirectoryEntry.Properties["objectSid"].Value;
                        SecurityIdentifier identifier = new SecurityIdentifier(objectSid, 0);
                        securityIdentifiers.Add(identifier);
                    }
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
            string path = GetDomainPathWithProtocol(domain);
            using (DirectoryEntry domainEntry = new DirectoryEntry(path))
            using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
            {
                return searcher.FindPrincipal<T>(accountName);
            }
        }

        protected DirectoryEntry GetDirectoryEntryByNativeGuid(Guid nativeGuid)
        {
            IEnumerable<string> domains = GetAllDomainsWithProtocol();
            foreach (string path in domains)
            {
                const string guidFilterFormat = "{0}/<GUID={1}>";
                string pathWithGuid = string.Format(guidFilterFormat, path, nativeGuid.ToString("N"));
                try
                {
                    if (DirectoryEntry.Exists(pathWithGuid))
                    {
                        prioritizedDomainPaths[path] = 1;
                        return new DirectoryEntry(pathWithGuid);
                    }
                }
                catch (COMException)
                {
                    if (!prioritizedDomainPaths.ContainsKey(path))
                    {
                        prioritizedDomainPaths[path] = 0;
                    }
                    prioritizedDomainPaths[path] -= 1;
                }
            }
            return null; // Todo: Not found exception
        }

        protected virtual IPrincipal GetPrincipalInternal(SecurityIdentifier sid, ICollection<string> additionalPropertyNames)
        {
            foreach (string path in GetAllDomainsWithProtocol())
            {
                try
                {
                    using (DirectoryEntry entry = SearchMember(path, sid))
                    {
                        if (entry != null)
                        {
                            Principal principal = Principal.FromDirectoryEntry(new DomainPath(path), entry, additionalPropertyNames);
                            prioritizedDomainPaths[path] = 1;
                            return principal;
                        }
                    }
                }
                catch (COMException)
                {
                    if (!prioritizedDomainPaths.ContainsKey(path))
                    {
                        prioritizedDomainPaths[path] = 0;
                    }
                    prioritizedDomainPaths[path] -= 1;
                }
            }

            return null; // Todo: Exception?
        }
        
        private string GetDomainPathWithProtocol(string domain = null)
        {
            if (string.IsNullOrEmpty(domain) || DefaultDomain.Equals(domain, StringComparison.CurrentCultureIgnoreCase))
            {
                return domainPath.GetPathWithProtocol();
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

        private string ResolveDomainPath(string domain)
        {
            string filter = string.Format("(flatName={0})", domain);
            string foreignDomain = ResolveForeignDomains(domainPath.GetPathWithProtocol(), filter, "name").First();
            return new DomainPath(foreignDomain).GetPathWithProtocol();
        }

        private IEnumerable<string> GetAllDomainsWithProtocol()
        {
            string defaultDomainPath = domainPath.GetPathWithProtocol();
            Dictionary<string, int> prioritizedPaths = new Dictionary<string, int>(prioritizedDomainPaths);

            // Yield paths which have been verified to contain valid principals
            foreach (string path in prioritizedPaths.Where(o => o.Value >= 0).Select(o => o.Key))
            {
                yield return path;
            }

            // Yield default domain path
            if (!prioritizedPaths.ContainsKey(defaultDomainPath))
            {
                prioritizedPaths[defaultDomainPath] = 1;
                yield return defaultDomainPath;
            }

            // Resolve path from AD and yield new
            string filter = "(objectClass=trustedDomain)";
            List<string> domains = ResolveForeignDomains(defaultDomainPath, filter, "name").ToList();
            foreach (string path in domains.Select(o => new DomainPath(o).GetPathWithProtocol()))
            {
                if (!prioritizedPaths.ContainsKey(path))
                {
                    prioritizedPaths[path] = 1;
                    yield return path;
                }
            }

            // Yield the rest of the paths
            foreach (string path in prioritizedPaths.Where(o => o.Value < 0).OrderByDescending(o => o.Value).Select(o => o.Key))
            {
                yield return path;
            }
        }

        private IEnumerable<string> ResolveForeignDomains(string path, string filter, string property)
        {
            List<string> domains = new List<string>();

            using (DirectoryEntry rootDirEntry = new DirectoryEntry(string.Format("{0}/RootDSE", path)))
            {
                string distinguishedName = (string)rootDirEntry.Properties["defaultNamingContext"].Value;

                using (DirectoryEntry entry = new DirectoryEntry(string.Format("{0}/cn=system,{1}", path, distinguishedName)))
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = filter;
                    searcher.PropertiesToLoad.Add(property);
                    SearchResultCollection results = searcher.FindAll();

                    foreach (SearchResult result in results)
                    {
                        domains.Add(result.Properties[property][0].ToString());
                    }
                }
            }

            return domains;
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
    }
}