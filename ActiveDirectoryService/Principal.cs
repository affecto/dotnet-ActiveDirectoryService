using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;

namespace Affecto.ActiveDirectoryService
{
    internal class Principal : IPrincipal
    {
        public string DomainPath { get; private set; }
        public string AccountName {get; private set;}
        public string DisplayName { get; private set; }
        public Guid NativeGuid { get; private set; }
        public bool IsGroup { get; private set; }
        public IDictionary<string, object> AdditionalProperties { get; private set; }
        internal IEnumerable<string> ChildDomainPaths { get; private set; }

        private Principal()
        {
        }

        public static Principal FromDirectoryEntry(DirectoryEntry directoryEntry, ICollection<string> additionalPropertyNames = null)
        {
            if (directoryEntry == null)
            {
                throw new ArgumentNullException("directoryEntry");
            }
            if (directoryEntry.Properties[ActiveDirectoryProperties.AccountName].Value == null)
            {
                throw new ActiveDirectoryException("Account name property not found in active directory entry.");
            }

            var principal = new Principal
            {
                AccountName = directoryEntry.Properties[ActiveDirectoryProperties.AccountName].Value.ToString(),
                NativeGuid = new Guid(directoryEntry.NativeGuid),
                DomainPath = directoryEntry.Path,
                IsGroup = directoryEntry.SchemaClassName == ActiveDirectoryProperties.AccountGroup,
            };

            principal.DisplayName = GetDisplayName(directoryEntry) ?? principal.AccountName;
            principal.AdditionalProperties = GetAdditionalProperties(directoryEntry, additionalPropertyNames);
            principal.ChildDomainPaths = GetChildDomainPaths(directoryEntry);

            return principal;
        }

        private static string GetDisplayName(DirectoryEntry directoryEntry)
        {
            object displayNameValue = directoryEntry.Properties[ActiveDirectoryProperties.DisplayName].Value;
            return displayNameValue != null ? displayNameValue.ToString() : null;
        }

        private static Dictionary<string, object> GetAdditionalProperties(DirectoryEntry directoryEntry, IEnumerable<string> additionalPropertyNames)
        {
            var results = new Dictionary<string, object>();

            if (additionalPropertyNames != null)
            {
                foreach (string propertyName in additionalPropertyNames)
                {
                    PropertyValueCollection propertyValueCollection = directoryEntry.Properties[propertyName];
                    if (propertyValueCollection != null)
                    {
                        results.Add(propertyName, propertyValueCollection.Value);
                    }
                }
            }

            return results;
        }

        private static List<string> GetChildDomainPaths(DirectoryEntry directoryEntry)
        {
            var memberPaths = new List<string>();
            PropertyValueCollection memberValueCollection = directoryEntry.Properties[ActiveDirectoryProperties.Member];

            if (memberValueCollection != null)
            {
                IEnumerator memberEnumerator = memberValueCollection.GetEnumerator();
                while (memberEnumerator.MoveNext())
                {
                    if (memberEnumerator.Current != null)
                    {
                        memberPaths.Add(AdDomainPathHandler.Escape(memberEnumerator.Current.ToString()));
                    }
                }
            }

            return memberPaths;
        }
    }
}