using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;

namespace Vitorize.Infrastructure.Interceptors
{
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentUserService _currentUserService;

        public AuditSaveChangesInterceptor(
            ICurrentUserService currentUserService)
        {
            _currentUserService = currentUserService;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            AddAuditLogs(eventData.Context);

            return base.SavingChangesAsync(
                eventData,
                result,
                cancellationToken);
        }

        private void AddAuditLogs(DbContext? context)
        {
            if (context == null)
                return;

            var entries = context.ChangeTracker
                .Entries()
                .Where(x =>
                    x.Entity is not AuditLog &&
                    x.Entity is not ErrorLog &&
                    x.Entity is not SecurityLog &&
                    x.Entity is not OutboxMessage &&
                    x.Entity is not IdempotencyKey &&
                    x.Entity is not PaymentCallback &&
                    x.Entity is not FinancialAuditLog &&
                    x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            if (!entries.Any())
                return;

            foreach (var entry in entries)
            {
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUserService.UserId,
                    ActionType = GetActionType(entry.State),
                    EntityName = entry.Entity.GetType().Name,
                    EntityId = GetPrimaryKey(entry),
                    Data = CreateAuditData(entry),
                    IpAddress = _currentUserService.IpAddress,
                    UserAgent = _currentUserService.UserAgent,
                    CreatedAt = DateTime.UtcNow
                };

                context.Set<AuditLog>().Add(auditLog);
            }
        }

        private static string GetActionType(EntityState state)
        {
            return state switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown"
            };
        }

        private static string? GetPrimaryKey(EntityEntry entry)
        {
            var primaryKey = entry.Metadata.FindPrimaryKey();

            if (primaryKey == null)
                return null;

            var values = primaryKey.Properties
                .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Join(",", values);
        }

        private static string CreateAuditData(EntityEntry entry)
        {
            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var property in entry.Properties)
            {
                var propertyName = property.Metadata.Name;

                if (property.Metadata.IsPrimaryKey())
                    continue;

                if (IsSensitiveField(propertyName))
                    continue;

                if (entry.State == EntityState.Added)
                {
                    after[propertyName] = property.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    before[propertyName] = property.OriginalValue;
                }
                else if (entry.State == EntityState.Modified &&
                         property.IsModified)
                {
                    before[propertyName] = property.OriginalValue;
                    after[propertyName] = property.CurrentValue;
                    changedColumns.Add(propertyName);
                }
            }

            var data = new
            {
                Before = before,
                After = after,
                ChangedColumns = changedColumns
            };

            return JsonSerializer.Serialize(data);
        }

        private static bool IsSensitiveField(string propertyName)
        {
            var name = propertyName.ToLowerInvariant();

            return name.Contains("password") ||
                   name.Contains("token") ||
                   name.Contains("hash") ||
                   name.Contains("encrypted") ||
                   name.Contains("secret") ||
                   name.Contains("key") ||
                   name.Contains("nationalcode") ||
                   name.Contains("bankcard") ||
                   name.Contains("shaba") ||
                   name.Contains("address") ||
                   name.Contains("postal") ||
                   name.Contains("birthdate") ||
                   name.Contains("deliveredcontent") ||
                   name.Contains("rawrequest") ||
                   name.Contains("rawresponse") ||
                   name.Contains("callbackdata") ||
                   name.Contains("filepath") ||
                   name.Contains("mobile") ||
                   name.Contains("email") ||
                   name.Contains("fullname") ||
                   name.Contains("adminnote");
        }
    }
}
