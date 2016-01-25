using System;
using System.DirectoryServices;

namespace Affecto.ActiveDirectoryService
{
    internal class Principal : IPrincipal
    {
        public PropertyValueCollection MemberValueCollection { get; private set; }

        public string DomainPath { get; private set; }

        public string Id {get; private set;}

        public string DisplayName { get; private set; }

        public string NativeGuid { get; private set; }

        public bool IsGroup { get; private set; }

        public Principal(DirectoryEntry directoryEntry)
        {
            if (directoryEntry == null)
            {
                throw new ArgumentNullException("directoryEntry");
            }
            if (directoryEntry.Properties[ActiveDirectoryProperties.AccountNameProperty].Value == null)
            {
                throw new ActiveDirectoryException("Account name property not found in active directory entry.");
            }

            Id = directoryEntry.Properties[ActiveDirectoryProperties.AccountNameProperty].Value.ToString();

            object displayNameValue = directoryEntry.Properties[ActiveDirectoryProperties.DisplayNameProperty].Value;
            DisplayName = displayNameValue != null ? displayNameValue.ToString() : Id;

            NativeGuid = directoryEntry.NativeGuid;
            DomainPath = directoryEntry.Path;

            IsGroup = directoryEntry.SchemaClassName == ActiveDirectoryProperties.AccountGroupProperty;
            MemberValueCollection = directoryEntry.Properties[ActiveDirectoryProperties.MemberProperty];
        }
    }
}