using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class PrincipalSearcher : DirectorySearcher
    {
        private readonly DomainPath domainPath;
        private readonly ICollection<string> additionalPropertyNames;

        public PrincipalSearcher(DomainPath domainPath, DirectoryEntry searchRoot, ICollection<string> additionalPropertyNames = null)
            : base(searchRoot)
        {
            this.domainPath = domainPath;
            this.additionalPropertyNames = additionalPropertyNames;

            PropertiesToLoad.Add(ActiveDirectoryProperties.ObjectGuid);
            PropertiesToLoad.Add(ActiveDirectoryProperties.DisplayName);
            PropertiesToLoad.Add(ActiveDirectoryProperties.Member);
            PropertiesToLoad.Add(ActiveDirectoryProperties.MemberOf);

            if (additionalPropertyNames != null)
            {
                foreach (string propertyName in additionalPropertyNames)
                {
                    PropertiesToLoad.Add(propertyName);
                }
            }
        }

        public T FindPrincipal<T>(string principalAccountName) where T : Principal
        {
            if (string.IsNullOrWhiteSpace(principalAccountName))
            {
                throw new ArgumentNullException("principalAccountName");
            }

            string userWithoutDomain = principalAccountName.Contains('\\') ? principalAccountName.Split('\\')[1] : principalAccountName;
            string filter = string.Format("({0}={1})", ActiveDirectoryProperties.AccountName, userWithoutDomain);

            T principal = FindSinglePrincipal<T>(filter);

            if (principal == null)
            {
                throw new ActiveDirectoryException(string.Format("Principal '{0}' not found in active directory.", principalAccountName));
            }

            return principal;
        }

        public IReadOnlyCollection<T> FindPrincipals<T>(string ldapFilter) where T : Principal
        {
            return FindAllPrincipals<T>(ldapFilter);
        }

        protected T FindSinglePrincipal<T>(string filter) where T : Principal
        {
            Filter = filter;
            SearchResult searchResult = FindOne();

            if (searchResult == null)
            {
                return null;
            }

            return MapToPrincipal<T>(searchResult);
        }

        protected List<T> FindAllPrincipals<T>(string filter) where T : Principal
        {
            Filter = filter;
            SearchResultCollection searchResults = FindAll();

            return searchResults.Cast<SearchResult>().Select(MapToPrincipal<T>).ToList();
        }

        private T MapToPrincipal<T>(SearchResult searchResult) where T : Principal
        {
            using (DirectoryEntry directoryEntry = searchResult.GetDirectoryEntry())
            {
                return Principal.FromDirectoryEntry<T>(domainPath, directoryEntry, additionalPropertyNames);
            }
        }
    }
}