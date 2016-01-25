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
            List<IPrincipal> principals = service.GetGroupMembers("", true, new List<string> {  }).ToList();
            IPrincipal principal = service.GetUser("", new List<string> { });
            bool isGroupMember = service.IsGroupMember("", "");
        }
    }
}