using System;
using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public interface IPrincipal
    {
        string AccountName { get; }
        string DisplayName { get; }
        Guid NativeGuid { get; }
        string DomainPath { get; }
        bool IsGroup { get; }
        IDictionary<string, object> AdditionalProperties { get; }
    }
}