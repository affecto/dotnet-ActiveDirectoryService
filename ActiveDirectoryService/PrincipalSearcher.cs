using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class PrincipalSearcher : DirectorySearcher
    {
        private readonly string principalAccountName;
        private readonly ICollection<string> additionalPropertyNames;

        public PrincipalSearcher(DirectoryEntry searchRoot, string principalAccountName, ICollection<string> additionalPropertyNames = null)
            : base(searchRoot)
        {
            if (string.IsNullOrWhiteSpace(principalAccountName))
            {
                throw new ArgumentNullException("principalAccountName");
            }

            this.principalAccountName = principalAccountName;
            this.additionalPropertyNames = additionalPropertyNames;

            string userWithoutDomain = principalAccountName.Contains('\\') ? principalAccountName.Split('\\')[1] : principalAccountName;
            Filter = string.Format("({0}={1})", ActiveDirectoryProperties.AccountName, userWithoutDomain);

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

        public Principal Find()
        {
            SearchResult searchResult = FindOne();
            if (searchResult == null)
            {
                throw new ActiveDirectoryException(string.Format("Principal '{0}' not found in active directory.", principalAccountName));
            }

            using (DirectoryEntry directoryEntry = searchResult.GetDirectoryEntry())
            {
                return Principal.FromDirectoryEntry(directoryEntry, additionalPropertyNames);
            }
        }
    }
}