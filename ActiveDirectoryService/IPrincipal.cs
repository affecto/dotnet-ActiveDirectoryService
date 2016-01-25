namespace Affecto.ActiveDirectoryService
{
    public interface IPrincipal
    {
        string Id { get; }
        string DisplayName { get; }
        string NativeGuid { get; }
        string DomainPath { get; }
        bool IsGroup { get; }
    }
}
