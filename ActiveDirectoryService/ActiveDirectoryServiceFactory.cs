using System;
using System.Collections.Generic;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    public static class ActiveDirectoryServiceFactory
    {
        public static IActiveDirectoryService CreateActiveDirectoryService(string domainPath, params string[] additionalDomainPaths)
        {
            IEnumerable<DomainPath> paths = new[] { domainPath }.Concat(additionalDomainPaths).Select(o => new DomainPath(o));
            return new ActiveDirectoryService(paths);
        }

        public static IActiveDirectoryService CreateCachedActiveDirectoryService(string domainPath, TimeSpan cacheDuration, params string[] additionalDomainPaths)
        {
            IEnumerable<DomainPath> paths = new[] { domainPath }.Concat(additionalDomainPaths).Select(o => new DomainPath(o));
            return new CachedActiveDirectoryService(paths, cacheDuration);
        }
    }
}