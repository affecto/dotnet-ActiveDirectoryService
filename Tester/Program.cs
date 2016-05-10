using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;

namespace Affecto.ActiveDirectoryService.Tester
{
    class Program
    {
        static void Main()
        {
            IActiveDirectoryService service = ActiveDirectoryServiceFactory.CreateCachedActiveDirectoryService(ConfigurationManager.AppSettings["domainPath"], TimeSpan.FromMinutes(5));

            List<IPrincipal> principals = service.SearchPrincipals("(&(objectClass=person)(sn=*)(company=*)(!userAccountControl:1.2.840.113556.1.4.803:=2)(department=*)(title=*))",
                new List<string> { "extensionAttribute8", "department", "company", "division" })
                .ToList();

            foreach (IPrincipal principal in principals.OrderBy(p => p.DisplayName))
            {
                Debug.WriteLine(principal.DisplayName);
            }

            List<IPrincipal> principals2 = service.SearchPrincipals("(&(objectClass=person)(sn=*)(company=*)(!userAccountControl:1.2.840.113556.1.4.803:=2)(department=*)(title=*))",
                new List<string> { "extensionAttribute8", "department", "company", "division" })
                .ToList();

            //IActiveDirectoryService service = ActiveDirectoryServiceFactory.CreateCachedActiveDirectoryService(ConfigurationManager.AppSettings["domainPath"],
            //    TimeSpan.FromMinutes(5));

            //Task<List<IPrincipal>> task = Task.Factory.StartNew(() => service.GetGroupMembers("turku-list", true, new List<string> { "thumbnailPhoto" }).ToList());

            //System.Threading.Thread.Sleep(2000);

            //IPrincipal principal = service.GetPrincipal("valtojoh", new List<string> { "thumbnailPhoto" });

            //List<IPrincipal> principals = service.GetGroupMembers("turku-list", true, new List<string> { }).ToList();


            ////IPrincipal principal = service.GetPrincipal("", new List<string> { });
            //bool isGroupMember = service.IsGroupMember("valtojoh", "turku-list");

            //List<IPrincipal> principals2 = service.GetGroupMembers("turku-list", true, new List<string> { }).ToList();

            //task.Wait();
        }
    }
}