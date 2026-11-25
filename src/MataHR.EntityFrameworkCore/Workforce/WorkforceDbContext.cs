using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using MataHR.Workforce;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;

namespace MataHR.Persistence;
public class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options) : AbpDbContext<WorkforceDbContext>(options)
{
    public DbSet<Volo.Abp.Identity.IdentityUser> Users => Set<Volo.Abp.Identity.IdentityUser>();
    public DbSet<Volo.Abp.Identity.IdentityRole> Roles => Set<Volo.Abp.Identity.IdentityRole>();
    public DbSet<Worker> WorkerRows => Set<Worker>();
    public DbSet<WorkforceUser> WorkforceUserRows => Set<WorkforceUser>();
    public DbSet<Office> OfficeRows => Set<Office>();
    public DbSet<Team> TeamRows => Set<Team>();
    public DbSet<Job> JobRows => Set<Job>();
    public DbSet<ChartCard> ChartCardRows => Set<ChartCard>();
    public DbSet<EmploymentChange> EmploymentChangeRows => Set<EmploymentChange>();
    public DbSet<WorkContract> WorkContractRows => Set<WorkContract>();
    public DbSet<SecureDocument> SecureDocumentRows => Set<SecureDocument>();
    public DbSet<ClockLocation> ClockLocationRows => Set<ClockLocation>();
    public DbSet<WorkSchedule> WorkScheduleRows => Set<WorkSchedule>();
    public DbSet<WorkSession> WorkSessionRows => Set<WorkSession>();
    public DbSet<TimeRequest> TimeRequestRows => Set<TimeRequest>();
    public DbSet<AbsencePolicy> AbsencePolicyRows => Set<AbsencePolicy>();
    public DbSet<PublicHoliday> PublicHolidayRows => Set<PublicHoliday>();
    public DbSet<LeaveBalanceEntry> LeaveBalanceEntries => Set<LeaveBalanceEntry>();
    public DbSet<LeaveAllocation> LeaveAllocationRows => Set<LeaveAllocation>();
    public DbSet<Absence> AbsenceRows => Set<Absence>();
    public DbSet<Compensation> CompensationRows => Set<Compensation>();
    public DbSet<EmployeeBenefit> EmployeeBenefitRows => Set<EmployeeBenefit>();
    public DbSet<PayRun> PayRunRows => Set<PayRun>();
    public DbSet<ReleasedPayslip> ReleasedPayslipRows => Set<ReleasedPayslip>();
    public DbSet<WorkforceAudit> WorkforceAuditRows => Set<WorkforceAudit>();
    public DbSet<PrivacyAcceptance> PrivacyAcceptanceRows => Set<PrivacyAcceptance>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ConfigureIdentity();
        b.ConfigurePermissionManagement();
        b.ConfigureAuditLogging();
        b.Entity<Compensation>().Property(x => x.EffectiveFrom).HasDefaultValue(new DateOnly(1, 1, 1));
        b.Entity<EmployeeBenefit>().Ignore(x => x.Active);
        b.Entity<EmployeeBenefit>().Property<bool>("LegacyActive").HasColumnName("Active").HasDefaultValue(true);
        b.Entity<WorkContract>().Ignore(x => x.State);
        b.Entity<Absence>().Ignore(x => x.WorkingDates);
        foreach (var type in typeof(Worker).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IWorkforceEntity).IsAssignableFrom(t)).Append(typeof(WorkforceUser)))
        {
            var e = b.Entity(type);
            e.ToTable("Mata" + type.Name);
            e.ConfigureByConvention();
            if (typeof(WorkforceEntity).IsAssignableFrom(type))
                e.Property<string>("ExtraProperties").IsRequired().HasDefaultValue("{}");
            e.Property(nameof(WorkforceRow.TenantId)).IsRequired();
        }

        b.Entity<Worker>().Property(x => x.Number).HasMaxLength(64);
        b.Entity<Worker>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Worker>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<Worker>().HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
        b.Entity<Worker>().HasIndex(x => new { x.TenantId, x.ManagerId });
        b.Entity<WorkforceUser>().Property(x => x.Email).HasMaxLength(254);
        b.Entity<WorkforceUser>().HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        b.Entity<WorkforceUser>().Property(x => x.PasswordHash).HasMaxLength(512);
        b.Entity<WorkforceUser>().Property(x => x.Roles).HasMaxLength(100);
        b.Entity<Office>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Team>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Job>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<Office>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Entity<Team>().HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
        b.Entity<Job>().HasIndex(x => new { x.TenantId, x.DepartmentId, x.Name }).IsUnique();
        foreach (var t in new[]
        {
            typeof(Worker),
            typeof(Office),
            typeof(Team),
            typeof(Job),
            typeof(ClockLocation),
            typeof(AbsencePolicy),
            typeof(WorkSession),
            typeof(SecureDocument),
            typeof(PayRun)
        }

        )
            b.Entity(t).HasAlternateKey(nameof(WorkforceRow.TenantId), nameof(WorkforceRow.Id));
        b.Entity<Team>().HasOne<Office>().WithMany().HasForeignKey(x => new { x.TenantId, x.BranchId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Job>().HasOne<Team>().WithMany().HasForeignKey(x => new { x.TenantId, x.DepartmentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Worker>().HasOne<Job>().WithMany().HasForeignKey(x => new { x.TenantId, x.PositionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Worker>().HasOne<Worker>().WithMany().HasForeignKey(x => new { x.TenantId, x.ManagerId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkSession>().Property(x => x.Status).HasMaxLength(32);
        b.Entity<WorkSession>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique().HasFilter("[Status] = 'Open'");
        b.Entity<WorkSession>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.Date });
        b.Entity<Absence>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.Start });
        b.Entity<WorkSession>().HasOne<ClockLocation>().WithMany().HasForeignKey(x => new { x.TenantId, x.LocationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Absence>().HasOne<AbsencePolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveAllocation>().HasOne<AbsencePolicy>().WithMany().HasForeignKey(x => new { x.TenantId, x.PolicyId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        EmployeeChild<WorkforceUser>(b);
        EmployeeChild<ChartCard>(b);
        EmployeeChild<EmploymentChange>(b);
        EmployeeChild<WorkContract>(b);
        EmployeeChild<SecureDocument>(b);
        EmployeeChild<WorkSchedule>(b);
        EmployeeChild<WorkSession>(b);
        EmployeeChild<TimeRequest>(b);
        EmployeeChild<LeaveAllocation>(b);
        EmployeeChild<Absence>(b);
        EmployeeChild<Compensation>(b);
        EmployeeChild<EmployeeBenefit>(b);
        EmployeeChild<ReleasedPayslip>(b);
        b.Entity<TimeRequest>().HasOne<WorkSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.SessionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReleasedPayslip>().HasOne<PayRun>().WithMany().HasForeignKey(x => new { x.TenantId, x.RunId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReleasedPayslip>().HasOne<SecureDocument>().WithMany().HasForeignKey(x => new { x.TenantId, x.DocumentId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WorkContract>().Property(x => x.Reference).HasMaxLength(100);
        b.Entity<WorkContract>().HasIndex(x => new { x.TenantId, x.Reference, x.Version }).IsUnique();
        b.Entity<SecureDocument>().Property(x => x.Name).HasMaxLength(180);
        b.Entity<SecureDocument>().Property(x => x.Content).HasMaxLength(5 * 1024 * 1024);
        b.Entity<SecureDocument>().Property(x => x.Sha256).HasMaxLength(64);
        b.Entity<ChartCard>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();
        b.Entity<WorkSchedule>().HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();
        b.Entity<Compensation>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveFrom }).IsUnique();
        b.Entity<LeaveAllocation>().HasAlternateKey(x => new { x.TenantId, x.Id });
        b.Entity<LeaveAllocation>().HasMany(x => x.Entries).WithOne().HasForeignKey(x => new { x.TenantId, x.AllocationId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LeaveAllocation>().Navigation(x => x.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Entity<AbsencePolicy>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<AbsencePolicy>().HasIndex(x => new { x.TenantId, x.Name, x.Version }).IsUnique();
        b.Entity<LeaveAllocation>().HasIndex(x => new { x.TenantId, x.EmployeeId, x.PolicyId, x.Year }).IsUnique();
        b.Entity<PublicHoliday>().HasIndex(x => new { x.TenantId, x.Date }).IsUnique();
        b.Entity<PayRun>().Property(x => x.Period).HasMaxLength(7);
        b.Entity<PayRun>().HasIndex(x => new { x.TenantId, x.Period }).IsUnique();
        b.Entity<ReleasedPayslip>().HasIndex(x => new { x.TenantId, x.RunId, x.EmployeeId }).IsUnique();
        b.Entity<PrivacyAcceptance>().Property(x => x.Version).HasMaxLength(40);
        b.Entity<PrivacyAcceptance>().HasIndex(x => new { x.TenantId, x.AccountId, x.Version }).IsUnique();
        foreach (var e in b.Model.GetEntityTypes().Where(e => typeof(IWorkforceEntity).IsAssignableFrom(e.ClrType)))
            foreach (var p in e.GetProperties().Where(p => p.ClrType == typeof(decimal)))
            {
                p.SetPrecision(18);
                p.SetScale(2);
            }
    }

    static void EmployeeChild<T>(ModelBuilder b)
        where T : class, IWorkforceEntity => b.Entity<T>().HasOne<Worker>().WithMany().HasForeignKey("TenantId", "EmployeeId").HasPrincipalKey(nameof(Worker.TenantId), nameof(Worker.Id)).OnDelete(DeleteBehavior.Restrict);
}

public class WorkforceDesignFactory : IDesignTimeDbContextFactory<WorkforceDbContext>
{
    public WorkforceDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WorkforceDbContext>().UseSqlServer("Server=localhost,14336;Database=MataHR;Integrated Security=True;TrustServerCertificate=True").Options);
}
