using System;
using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public interface IActiveDirectoryService
    {
        IPrincipal GetPrincipal(string accountName, ICollection<string> additionalPropertyNames = null);
        IPrincipal GetPrincipal(Guid nativeGuid, ICollection<string> additionalPropertyNames = null);
        bool IsGroupMember(string accountName, string groupName);
        IReadOnlyCollection<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null);
        IReadOnlyCollection<IPrincipal> GetGroupMembers(Guid nativeGuid, bool recursive, ICollection<string> additionalPropertyNames = null);
        IReadOnlyCollection<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null);
        IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMember(string userAccountName);
        IReadOnlyCollection<IPrincipal> GetGroupsWhereUserIsMember(Guid userNativeGuid);
    }
}