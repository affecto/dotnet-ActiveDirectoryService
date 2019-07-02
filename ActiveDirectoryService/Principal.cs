using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    internal abstract class Principal : IPrincipal
    {
        protected readonly DomainPath domainPath;

        public abstract bool IsGroup { get; }
        public abstract bool IsActive { get; }
        public string DomainPath { get; private set; }
        public string DomainName { get; private set; }
        public string AccountName { get; private set; }
        public string DisplayName { get; private set; }
        public Guid NativeGuid { get; private set; }
        public IDictionary<string, object> AdditionalProperties { get; private set; }

        protected Principal(DomainPath domainPath, DirectoryEntry directoryEntry, ICollection<string> additionalPropertyNames)
        {
            this.domainPath = domainPath;

            AccountName = directoryEntry.Properties[ActiveDirectoryProperties.AccountName].Value.ToString();
            NativeGuid = new Guid(directoryEntry.NativeGuid);
            DomainPath = directoryEntry.Path;
            DomainName = GetDomainName(domainPath);
            DisplayName = GetDisplayName(directoryEntry) ?? AccountName;
            AdditionalProperties = GetAdditionalProperties(directoryEntry, additionalPropertyNames);
        }

        public static Principal FromDirectoryEntry(DomainPath domainPath, DirectoryEntry directoryEntry, ICollection<string> additionalPropertyNames = null)
        {
            if (directoryEntry == null)
            {
                throw new ArgumentNullException("directoryEntry");
            }
            if (directoryEntry.Properties[ActiveDirectoryProperties.AccountName].Value == null)
            {
                throw new ActiveDirectoryException("Account name property not found in active directory entry.");
            }

            bool isGroup = directoryEntry.SchemaClassName == ActiveDirectoryProperties.AccountGroup;

            if (isGroup)
            {
                return new GroupPrincipal(domainPath, directoryEntry, additionalPropertyNames);
            }

            return new UserPrincipal(domainPath, directoryEntry, additionalPropertyNames);
        }

        public static T FromDirectoryEntry<T>(DomainPath domainPath, DirectoryEntry directoryEntry, ICollection<string> additionalPropertyNames = null) where T : Principal
        {
            Principal principal = FromDirectoryEntry(domainPath, directoryEntry, additionalPropertyNames);

            if (principal is T)
            {
                return (T)principal;
            }

            throw new InvalidCastException(string.Format("Could not cast principal '{0}' to type '{1}'.", principal.AccountName, typeof(T).FullName));
        }

        public override string ToString()
        {
            return string.Format("{0} - {1}", GetType().FullName, AccountName);
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

        private static string GetDomainName(DomainPath domainPath)
        {
            string[] splittedDomainPath = domainPath.Value.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);

            if (splittedDomainPath.Length > 1)
            {
                return string.Join(".", splittedDomainPath.Take(splittedDomainPath.Length - 1));
            }
            else
            {
                return splittedDomainPath[0];
            }
        }
    }
}