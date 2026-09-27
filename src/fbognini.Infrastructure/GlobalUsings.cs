// Finbuckle 10 standardised namespaces to match folder layout, so the same types live in different
// namespaces on the two supported majors. Importing them globally keeps the divergence out of every file.
#if NET10_0_OR_GREATER
global using Finbuckle.MultiTenant.AspNetCore.Extensions;
global using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
global using Finbuckle.MultiTenant.EntityFrameworkCore.Stores;
global using Finbuckle.MultiTenant.Extensions;
#else
global using Finbuckle.MultiTenant.EntityFrameworkCore.Stores.EFCoreStore;
#endif
global using Finbuckle.MultiTenant;
global using Finbuckle.MultiTenant.Abstractions;
global using Finbuckle.MultiTenant.EntityFrameworkCore;
