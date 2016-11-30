using System;
using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;

namespace Affecto.ActiveDirectoryService
{
    internal class UserPrincipal : Principal
    {
        private readonly bool isActive;

        public UserPrincipal(DomainPath domainPath, DirectoryEntry directoryEntry, ICollection<string> additionalPropertyNames)
            : base(domainPath, directoryEntry, additionalPropertyNames)
        {
            isActive = IsActiveUser(directoryEntry);
            ParentDomainPaths = GetParentDomainPaths(directoryEntry);
        }

        public override bool IsGroup
        {
            get { return false; }
        }

        public override bool IsActive
        {
            get { return isActive; }
        }

        internal IReadOnlyCollection<string> ParentDomainPaths { get; private set; }
        
        private List<string> GetParentDomainPaths(DirectoryEntry directoryEntry)
        {
            return GetParentDomainPaths(directoryEntry, new List<string>());
        }

        private List<string> GetParentDomainPaths(DirectoryEntry directoryEntry, List<string> parentPaths)
        {
            PropertyValueCollection memberValueCollection = directoryEntry.Properties[ActiveDirectoryProperties.MemberOf];

            if (memberValueCollection != null)
            {
                IEnumerator memberEnumerator = memberValueCollection.GetEnumerator();
                while (memberEnumerator.MoveNext())
                {
                    if (memberEnumerator.Current != null)
                    {
                        string currentValue = AdDomainPathHandler.Escape(memberEnumerator.Current.ToString());
                        if (!parentPaths.Contains(currentValue))
                        {
                            parentPaths.Add(currentValue);

                            string fullDomainPath = domainPath.GetPathWithProtocol() + "/" + currentValue;
                            using (var entry = new DirectoryEntry(fullDomainPath))
                            {
                                GetParentDomainPaths(entry, parentPaths);
                            }
                        }
                    }
                }
            }

            return parentPaths;
        }

        private static bool IsActiveUser(DirectoryEntry directoryEntry)
        {
            if (directoryEntry.NativeGuid == null)
            {
                return false;
            }

            int flags = (int) directoryEntry.Properties[ActiveDirectoryProperties.UserAccountControl].Value;
            return !Convert.ToBoolean(flags & 0x0002);
        }
    }
}