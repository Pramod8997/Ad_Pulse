using System.Text.Json;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;

namespace AdPulse.Api.Campaigns;

public interface IAuditEventWriter
{
    void Add(AdPulseDbContext db, TenantContext tenant, string action, Guid campaignId);
}

public sealed class AuditEventWriter : IAuditEventWriter
{
    public void Add(AdPulseDbContext db, TenantContext tenant, string action, Guid campaignId)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenant.TenantId,
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorSubjectId = tenant.SubjectId,
            Action = action,
            EntityType = "campaign",
            EntityId = campaignId.ToString("D"),
            PayloadJson = JsonSerializer.Serialize(new { })
        });
    }
}
