using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace Affecto.ActiveDirectoryService.Tester
{
    class Program
    {
        static void Main()
        {
            IActiveDirectoryService service = ActiveDirectoryServiceFactory.CreateActiveDirectoryService(ConfigurationManager.AppSettings["domainPath"]);
            List<IPrincipal> principals = service.GetGroupMembers("turku-list", true, new List<string> { "extensionAttribute8" }).ToList();
            IPrincipal principal = service.GetUser("valtojoh", new List<string> { "extensionAttribute8" });
            bool isGroupMember = service.IsGroupMember("valtojoh", "turku-list");
        }
    }
}