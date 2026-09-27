namespace fbognini.Core.Domain;

public interface IHaveTenant
{
    string TenantId { get; set; }
}
