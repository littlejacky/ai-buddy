using Buddy.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buddy.Api.Infrastructure.Persistence;

public class BuddyDbContext : DbContext
{
    public BuddyDbContext(DbContextOptions<BuddyDbContext> options) : base(options)
    {
    }

    public DbSet<Family> Families => Set<Family>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
    public DbSet<DeviceBinding> DeviceBindings => Set<DeviceBinding>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<ChildSessionLease> ChildSessionLeases => Set<ChildSessionLease>();
    public DbSet<MemoryFact> MemoryFacts => Set<MemoryFact>();
    public DbSet<SessionSummary> SessionSummaries => Set<SessionSummary>();
    public DbSet<OutboxJob> OutboxJobs => Set<OutboxJob>();
    public DbSet<SafetyEvent> SafetyEvents => Set<SafetyEvent>();
    public DbSet<BuddyActivity> BuddyActivities => Set<BuddyActivity>();
    public DbSet<ToolInvocation> ToolInvocations => Set<ToolInvocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ChildSessionLease composite / single PK
        modelBuilder.Entity<ChildSessionLease>()
            .HasKey(l => l.ChildProfileId);

        // Indexes for performance and isolation
        modelBuilder.Entity<Family>()
            .HasIndex(f => f.Status);

        modelBuilder.Entity<Guardian>()
            .HasIndex(g => new { g.FamilyId, g.SubjectIdentifier });

        modelBuilder.Entity<ChildProfile>()
            .HasIndex(c => new { c.FamilyId, c.IsActive });

        modelBuilder.Entity<DeviceBinding>()
            .HasIndex(d => new { d.FamilyId, d.DeviceIdentifier });

        modelBuilder.Entity<ConsentRecord>()
            .HasIndex(c => new { c.ChildProfileId, c.ConsentType });

        modelBuilder.Entity<Session>()
            .HasIndex(s => new { s.FamilyId, s.ChildProfileId, s.Status });

        modelBuilder.Entity<MemoryFact>()
            .HasIndex(m => new { m.ChildProfileId, m.IsActive, m.FactType });

        modelBuilder.Entity<SessionSummary>()
            .HasIndex(s => new { s.ChildProfileId, s.GeneratedAtUtc });

        modelBuilder.Entity<OutboxJob>()
            .HasIndex(o => new { o.Status, o.LockedUntilUtc, o.RetryCount });

        modelBuilder.Entity<SafetyEvent>()
            .HasIndex(s => new { s.ChildProfileId, s.Severity });

        // Seed default family and guardian for development / initial out-of-the-box experience
        var defaultFamilyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var defaultGuardianId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var defaultChildId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        modelBuilder.Entity<Family>().HasData(new Family
        {
            Id = defaultFamilyId,
            Name = "快乐一家",
            Status = "Active",
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<Guardian>().HasData(new Guardian
        {
            Id = defaultGuardianId,
            FamilyId = defaultFamilyId,
            SubjectIdentifier = "dev_parent@ai-buddy.local",
            DisplayName = "默认家长",
            Role = "Owner",
            PinHash = "123456", // Default PIN for dev
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        modelBuilder.Entity<ChildProfile>().HasData(new ChildProfile
        {
            Id = defaultChildId,
            FamilyId = defaultFamilyId,
            Nickname = "乐乐",
            AgeBand = "7-8",
            PreferredVoice = "marin",
            LanguagePreference = "zh-CN",
            CurrentMemoryVersion = 1,
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        // Seed baseline consents for the default child
        var consentTypes = new[]
        {
            ("VoiceService", true),
            ("CrossSessionMemory", true),
            ("ParentDiscoveryDigest", true),
            ("TranscriptProcessing", true),
            ("ChildSafetyAudit", true),
            ("ApproximateLocation", false),
            ("PublicWebSearch", false)
        };

        var seedConsents = consentTypes.Select((ct, idx) => new ConsentRecord
        {
            Id = Guid.Parse($"44444444-4444-4444-4444-44444444440{idx}"),
            GuardianId = defaultGuardianId,
            ChildProfileId = defaultChildId,
            ConsentType = ct.Item1,
            Version = "1.0",
            IsGranted = ct.Item2,
            GrantedAtUtc = ct.Item2 ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null
        }).ToArray();

        modelBuilder.Entity<ConsentRecord>().HasData(seedConsents);
    }
}
