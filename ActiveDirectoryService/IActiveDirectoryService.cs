using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public interface IActiveDirectoryService
    {
        IPrincipal GetUser(string userName, ICollection<string> additionalPropertyNames = null);
        bool IsGroupMember(string userName, string groupName);
        IEnumerable<IPrincipal> GetGroupMembers(string groupName, bool recursive, ICollection<string> additionalPropertyNames = null);
        IEnumerable<IPrincipal> SearchPrincipals(string ldapFilter, ICollection<string> additionalPropertyNames = null);
    }
}