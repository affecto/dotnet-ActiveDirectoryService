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
        string DomainName { get; }
        bool IsGroup { get; }
        bool IsActive { get; }
        IDictionary<string, object> AdditionalProperties { get; }
    }
}