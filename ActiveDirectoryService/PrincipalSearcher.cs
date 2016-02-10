using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class PrincipalSearcher : DirectorySearcher
    {
        private readonly ICollection<string> additionalPropertyNames;

        public PrincipalSearcher(DirectoryEntry searchRoot, ICollection<string> additionalPropertyNames = null)
            : base(searchRoot)
        {
            this.additionalPropertyNames = additionalPropertyNames;

            PropertiesToLoad.Add(ActiveDirectoryProperties.ObjectGuid);
            PropertiesToLoad.Add(ActiveDirectoryProperties.DisplayName);
            PropertiesToLoad.Add(ActiveDirectoryProperties.Member);

            if (additionalPropertyNames != null)
            {
                foreach (string propertyName in additionalPropertyNames)
                {
                    PropertiesToLoad.Add(propertyName);
                }
            }
        }

        public Principal FindPrincipal(string principalAccountName)
        {
            if (string.IsNullOrWhiteSpace(principalAccountName))
            {
                throw new ArgumentNullException("principalAccountName");
            }

            string userWithoutDomain = principalAccountName.Contains('\\') ? principalAccountName.Split('\\')[1] : principalAccountName;
            string filter = string.Format("({0}={1})", ActiveDirectoryProperties.AccountName, userWithoutDomain);

            Principal principal = FindSinglePrincipal(filter);

            if (principal == null)
            {
                throw new ActiveDirectoryException(string.Format("Principal '{0}' not found in active directory.", principalAccountName));
            }

            return principal;
        }

        public IEnumerable<Principal> FindPrincipals(string ldapFilter)
        {
            return FindAllPrincipals(ldapFilter);
        }

        protected Principal FindSinglePrincipal(string filter)
        {
            Filter = filter;
            SearchResult searchResult = FindOne();

            if (searchResult == null)
            {
                return null;
            }

            return MapToPrincipal(searchResult);
        }

        protected List<Principal> FindAllPrincipals(string filter)
        {
            Filter = filter;
            SearchResultCollection searchResults = FindAll();

            return searchResults.Cast<SearchResult>().Select(MapToPrincipal).ToList();
        }

        private Principal MapToPrincipal(SearchResult searchResult)
        {
            using (DirectoryEntry directoryEntry = searchResult.GetDirectoryEntry())
            {
                return Principal.FromDirectoryEntry(directoryEntry, additionalPropertyNames);
            }
        }
    }
}