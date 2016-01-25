using System;
using System.DirectoryServices;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal class PrincipalSearcher : DirectorySearcher
    {
        private readonly string principalAccountName;

        public PrincipalSearcher(DirectoryEntry searchRoot, string principalAccountName)
            : base(searchRoot)
        {
            if (string.IsNullOrWhiteSpace(principalAccountName))
            {
                throw new ArgumentNullException("principalAccountName");
            }
            this.principalAccountName = principalAccountName;

            string userWithoutDomain = principalAccountName.Contains('\\') ? principalAccountName.Split('\\')[1] : principalAccountName;
            Filter = string.Format("({0}={1})", ActiveDirectoryProperties.AccountNameProperty, userWithoutDomain);
            PropertiesToLoad.Add(ActiveDirectoryProperties.ObjectGuidProperty);
            PropertiesToLoad.Add(ActiveDirectoryProperties.DisplayNameProperty);
            PropertiesToLoad.Add(ActiveDirectoryProperties.MemberProperty);
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
                return new Principal(directoryEntry);
            }
        }
    }
}