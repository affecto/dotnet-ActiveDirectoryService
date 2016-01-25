using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public interface IActiveDirectoryService
    {
        IPrincipal GetUser(string userName);
        bool IsGroupMember(string userName, string groupName);
        IEnumerable<IPrincipal> GetGroupMemberPrincipals(string groupName, bool recursive);
    }
}