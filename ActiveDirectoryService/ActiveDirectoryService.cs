using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Principal;

namespace Affecto.ActiveDirectoryService
{
    internal class ActiveDirectoryService : IActiveDirectoryService
    {
        private const string LdapPathPrefix = "LDAP://";

        private readonly IReadOnlyCollection<DomainPath> domainPaths;
        private readonly IReadOnlyDictionary<DomainPath, string> domains;

        public ActiveDirectoryService(IEnumerable<DomainPath> domainPaths)
        {
            if (domainPaths == null)
            {
                throw new ArgumentNullException("domainPaths", "Domain path must be defined.");
            }

            this.domainPaths = domainPaths.ToList();

            if (!this.domainPaths.Any())
            {
                throw new ArgumentNullException("domainPaths", "No domain paths was given.");
            }

            domains = this.domainPaths.ToDictionary(o => o, GetDefaultDomain);
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

            foreach (DomainPath domainPath in domainPaths)
            {
                using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
                using (PrincipalSearcher principalSearcher = new PrincipalSearcher(domainPath, domainEntry))
                {
                    GroupPrincipal groupPrincipal = principalSearcher.FindPrincipal<GroupPrincipal>(groupName);
                    result.AddRange(ResolveMembers(groupPrincipal, recursive, additionalPropertyNames));
                }
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
            foreach (DomainPath domainPath in domainPaths)
            {
                using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
                using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
                {
                    IReadOnlyCollection<Principal> principals = searcher.FindPrincipals<Principal>(ldapFilter);
                    if (principals.Any())
                    {
                        return principals;
                    }
                }
            }

            return new List<IPrincipal>(0);
        }

        public virtual IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMember(Guid userNativeGuid)
        {
            using (DirectoryEntry domainEntry = GetDirectoryEntryByNativeGuid(userNativeGuid))
            {
                SecurityIdentifier sid = GetObjectSid(domainEntry);
                DomainPath domainPath = ResolveDomainPath(domainEntry);
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
            DomainPath domainPath = ResolveDomainPath(principal.DomainPath);

            foreach (string parentDomainPath in principal.ParentDomainPaths)
            {
                string path = string.Format("{0}/{1}", domainPath.GetPathWithProtocol(), parentDomainPath);
                using (var entry = new DirectoryEntry(path))
                {
                    groups.Add(Principal.FromDirectoryEntry(domainPath, entry));
                }
            }

            if (!groups.Select(p => p.AccountName).Any(accountName => accountName.ToLower().Contains("domain users")))
            {
                string domain = domains[domainPath];
                groups.Add(GetPrincipalInternal<GroupPrincipal>(string.Format("{0}\\Domain users", domain)));
            }

            return groups;
        }

        protected virtual IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMemberInternal(SecurityIdentifier sid)
        {
            List<IPrincipal> groups = new List<IPrincipal>();

            foreach (DomainPath domainPath in domainPaths)
            {
                string namingContext = GetNamingContext(domainPath.GetPathWithProtocol());
                string filter = string.Format("(&(member=CN={0},CN=ForeignSecurityPrincipals,{1}))", sid.Value, namingContext);

                using (DirectoryEntry entry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
                using (DirectorySearcher searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = filter;
                    searcher.PropertiesToLoad.Add(ActiveDirectoryProperties.DistinguishedName);
                    SearchResultCollection searchResults = searcher.FindAll();

                    foreach (SearchResult result in searchResults)
                    {
                        List<IPrincipal> parents = GetParentDomainPaths(GetPathWithProtocol(result.Path), result.Properties[ActiveDirectoryProperties.DistinguishedName][0].ToString());
                        groups.AddRange(parents.Where(o => groups.All(p => p.NativeGuid != o.NativeGuid)));
                    }
                }

                if (!groups.Any(o => o.AccountName.ToLower().Contains("domain users")))
                {
                    string domain = domains[domainPath];
                    groups.Add(GetPrincipalInternal<GroupPrincipal>(string.Format("{0}\\Domain users", domain)));
                }
            }

            return groups;
        }

        protected virtual List<IPrincipal> GetParentDomainPaths(string path, string distinguishedName)
        {
            List<IPrincipal> principals = new List<IPrincipal>();
            string fullDomainPath = path + "/" + distinguishedName;
            using (DirectoryEntry entry = new DirectoryEntry(fullDomainPath))
            {
                DomainPath domainPath = ResolveDomainPath(path);
                GroupPrincipal principal = Principal.FromDirectoryEntry(domainPath, entry) as GroupPrincipal;
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
            foreach (DomainPath domainPath in domainPaths)
            {
                using (var context = new PrincipalContext(ContextType.Domain, domainPath.GetPathWithoutProtocol()))
                using (var group = System.DirectoryServices.AccountManagement.GroupPrincipal.FindByIdentity(context, groupName))
                {
                    if (group != null)
                    {
                        return group.GetMembers(true).Select(member => member.SamAccountName).ToList();
                    }
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
                DomainPath domainPath = ResolveDomainPath(domainEntry);
                return Principal.FromDirectoryEntry<T>(domainPath, domainEntry, additionalPropertyNames);
            }
        }

        protected virtual T GetPrincipalInternal<T>(string accountName, ICollection<string> additionalPropertyNames = null) where T : Principal
        {
            string domain = GetDomain(accountName);

            foreach (DomainPath domainPath in domains.Where(o => domain == null || o.Value.Equals(domain, StringComparison.OrdinalIgnoreCase)).Select(o => o.Key))
            {
                using (DirectoryEntry domainEntry = new DirectoryEntry(domainPath.GetPathWithProtocol()))
                using (PrincipalSearcher searcher = new PrincipalSearcher(domainPath, domainEntry, additionalPropertyNames))
                {
                    try
                    {
                        return searcher.FindPrincipal<T>(accountName);
                    }
                    catch (ActiveDirectoryException)
                    {
                    }
                }
            }

            throw new ActiveDirectoryException(string.Format("Principal '{0}' not found in active directory.", accountName));
        }

        protected virtual IPrincipal GetPrincipalInternal(SecurityIdentifier sid, ICollection<string> additionalPropertyNames = null)
        {
            foreach (DomainPath domainPath in domainPaths)
            {
                using (DirectoryEntry entry = SearchMember(domainPath.GetPathWithProtocol(), sid))
                {
                    if (entry != null)
                    {
                        return Principal.FromDirectoryEntry(domainPath, entry, additionalPropertyNames);
                    }
                }
            }

            return null;
        }

        protected virtual string GetNamingContext(string path)
        {
            using (DirectoryEntry rootDirEntry = new DirectoryEntry(string.Format("{0}/RootDSE", path)))
            {
                return (string)rootDirEntry.Properties["defaultNamingContext"].Value;
            }
        }

        private DirectoryEntry GetDirectoryEntryByNativeGuid(Guid nativeGuid)
        {
            foreach (DomainPath domainPath in domainPaths)
            {
                const string guidFilterFormat = "{0}/<GUID={1}>";
                string pathWithGuid = string.Format(guidFilterFormat, domainPath.GetPathWithProtocol(), nativeGuid.ToString("N"));
                if (DirectoryEntry.Exists(pathWithGuid))
                {
                    return new DirectoryEntry(pathWithGuid);
                }
            }

            return null; // Todo: Not found exception
        }

        private string GetDefaultDomain(DomainPath domainPath)
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

        private DomainPath ResolveDomainPath(string path)
        {
            return domainPaths.First(o => path.StartsWith(o.GetPathWithProtocol(), StringComparison.OrdinalIgnoreCase));
        }

        private DomainPath ResolveDomainPath(DirectoryEntry entry)
        {
            return ResolveDomainPath(entry.Path);
        }
    }
}