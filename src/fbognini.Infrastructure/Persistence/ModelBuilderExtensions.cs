using fbognini.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace fbognini.Infrastructure.Persistence
{
    public static class ModelBuilderExtensions
    {

        public static void ApplyIdentityConfiguration<TUser, TRole>(this ModelBuilder modelBuilder, string authSchema)
            where TUser : IdentityUser<string>
            where TRole : IdentityRole<string>
        {
            modelBuilder.ApplyIdentityConfiguration<TUser, TRole, string>(authSchema);
        }

        public static void ApplyIdentityConfiguration<TUser, TRole, TKey>(this ModelBuilder modelBuilder, string authSchema)
            where TUser : IdentityUser<TKey>
            where TRole : IdentityRole<TKey>
            where TKey: IEquatable<TKey>
        {
            modelBuilder.Entity<TUser>().ToTable("Users", authSchema);
            modelBuilder.Entity<TRole>().ToTable("Roles", authSchema);

            modelBuilder.Entity<IdentityRoleClaim<TKey>>().ToTable("RoleClaims", authSchema);
            modelBuilder.Entity<IdentityUserRole<TKey>>().ToTable("UserRoles", authSchema);
            modelBuilder.Entity<IdentityUserClaim<TKey>>().ToTable("UserClaims", authSchema);
            modelBuilder.Entity<IdentityUserLogin<TKey>>().ToTable("UserLogins", authSchema);
            modelBuilder.Entity<IdentityUserToken<TKey>>().ToTable("UserTokens", authSchema);
        }


        public static void ApplyGlobalFilters<TInterface>(this ModelBuilder modelBuilder, Expression<Func<TInterface, bool>> expression)
        {
            foreach (var entity in modelBuilder.GetEntityTypesImplementing<TInterface>())
            {
                var newParam = Expression.Parameter(entity);
                var newbody = ReplacingExpressionVisitor.Replace(expression.Parameters.Single(), newParam, expression.Body);
                modelBuilder.Entity(entity).HasQueryFilter(Expression.Lambda(newbody, newParam));
            }
        }

        // Finbuckle merges its tenant filter with whatever HasQueryFilter already set, so every other global filter must be applied first.
        public static void ApplyMultiTenant<TInterface>(this ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.GetEntityTypesImplementing<TInterface>())
            {
                modelBuilder.Entity(entity).IsMultiTenant().AdjustUniqueIndexes();
            }
        }

        private static Type[] GetEntityTypesImplementing<TInterface>(this ModelBuilder modelBuilder)
        {
            return modelBuilder.Model
                .GetEntityTypes()
                .Where(e => e.ClrType.GetInterface(typeof(TInterface).Name) != null)
                .Select(e => e.ClrType)
                .ToArray();
        }
    }
}
