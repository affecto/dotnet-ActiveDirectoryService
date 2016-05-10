using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public interface IActiveDirectoryService
    {
        IPrincipal GetPrincipal(string accountName, ICollection<string> additionalPropertyNames = null);
        bool IsGroupMember(string accountName, string groupName);
        IEnumerable<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null);
        IEnumerable<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null);
    }
}