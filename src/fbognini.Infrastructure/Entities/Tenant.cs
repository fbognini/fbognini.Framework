using fbognini.Infrastructure.Multitenancy;
using System;

namespace fbognini.Infrastructure.Entities
{
    public class Tenant : ITenantInfo
    {
        public Tenant()
        {
        }

        public Tenant(string identifier, string name, string connectionString, string adminEmail, string? issuer = null)
        {
            Identifier = identifier;
            Name = name;
            ConnectionString = connectionString ?? string.Empty;
            AdminEmail = adminEmail;
            IsActive = true;
            Issuer = issuer;

            // Add Default 1 Month Validity for all new tenants. Something like a DEMO period for tenants.
            ValidUpto = DateTime.UtcNow.AddMonths(1);
        }

        /// <summary>
        /// The actual TenantId, which is also used in the TenantId shadow property on the multitenant entities.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The identifier that is used in headers/routes/querystrings.
        /// </summary>
        public string Identifier { get; set; } = default!;

        public string Name { get; set; } = default!;
        public string ConnectionString { get; set; } = default!;

        public string AdminEmail { get; set; } = default!;
        public bool IsActive { get; set; }
        public DateTime ValidUpto { get; set; }

        public string? OpenIdConnectAuthority { get; set; }
        public string? OpenIdConnectClientId { get; set; }
        public string? OpenIdConnectClientSecret { get; set; }

        /// <summary>
        /// Used by AzureAd Authorization to store the AzureAd Tenant Issuer to map against.
        /// </summary>
        public string? Issuer { get; set; }

        public void AddValidity(int months) =>
            ValidUpto = ValidUpto.AddMonths(months);

        public void SetValidity(in DateTime validTill) =>
            ValidUpto = ValidUpto < validTill
                ? validTill
                : throw new Exception("Subscription cannot be backdated.");

        public void Activate()
        {
            if (Identifier == MultitenancyConstants.Root.Key)
            {
                throw new InvalidOperationException("Invalid Tenant");
            }

            IsActive = true;
        }

        public void Deactivate()
        {
            if (Identifier == MultitenancyConstants.Root.Key)
            {
                throw new InvalidOperationException("Invalid Tenant");
            }

            IsActive = false;
        }

#if NET10_0_OR_GREATER

        // Finbuckle 10 narrowed ITenantInfo down to two read-only members.
        string ITenantInfo.Id => Id;
        string ITenantInfo.Identifier => Identifier;

#else

        string? ITenantInfo.Id { get => Id.ToString(); set => Id = value ?? throw new InvalidOperationException("Id can't be null."); }
        string? ITenantInfo.Identifier { get => Identifier; set => Identifier = value ?? throw new InvalidOperationException("Identifier can't be null."); }
        string? ITenantInfo.Name { get => Name; set => Name = value ?? throw new InvalidOperationException("Name can't be null."); }

#endif
    }
}