using System;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    public static class ActiveDirectoryServiceFactory
    {
        public static IActiveDirectoryService CreateActiveDirectoryService(params string[] domainPaths)
        {
            return new ActiveDirectoryService(domainPaths.Select(o => new DomainPath(o)));
        }

        public static IActiveDirectoryService CreateCachedActiveDirectoryService(TimeSpan cacheDuration, params string[] domainPaths)
        {
            return new CachedActiveDirectoryService(domainPaths.Select(o => new DomainPath(o)), cacheDuration);
        }
    }
}