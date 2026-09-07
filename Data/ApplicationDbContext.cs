using CloudMVCApplication.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudMVCApplication.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PropertyManagementCompany> PropertyManagementCompanies { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<TenantProfile> TenantProfiles { get; set; }
        public DbSet<TechnicianProfile> TechnicianProfiles { get; set; }
        public DbSet<TechnicianDocument> TechnicianDocuments { get; set; }
        public DbSet<AdministratorProfile> AdministratorProfiles { get; set; }
        public DbSet<ManagerProfile> ManagerProfiles { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<RequestImage> RequestImages { get; set; }
        public DbSet<RequestStatusHistory> RequestStatusHistories { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<ServicePayment> ServicePayments { get; set; }
        public DbSet<TechnicianPayment> TechnicianPayments { get; set; }
        public DbSet<TechnicianPayout> TechnicianPayouts { get; set; }
        public DbSet<SubscriptionPayment> SubscriptionPayments { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<CompanySubscription> CompanySubscriptions { get; set; }
        public DbSet<RequestFeedback> RequestFeedbacks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<CompanySubscriptionEvent> CompanySubscriptionEvents { get; set; }
        public DbSet<CompanyInvoice> CompanyInvoices { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PropertyManagementCompany>()
                .HasMany(c => c.Users)
                .WithOne(u => u.Company)
                .HasForeignKey(u => u.CompanyId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PropertyManagementCompany>()
                .HasMany(c => c.Properties)
                .WithOne(p => p.Company)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PropertyManagementCompany>(entity =>
            {
                entity.HasIndex(c => c.LegacyCustomerId);
                entity.HasIndex(c => c.LegacySubscriptionId).IsUnique();
                entity.HasIndex(c => c.XenditCustomerId);
                entity.HasIndex(c => c.XenditSubscriptionId).IsUnique();

                entity.HasMany(c => c.SubscriptionEvents)
                    .WithOne(e => e.Company)
                    .HasForeignKey(e => e.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.Invoices)
                    .WithOne(i => i.Company)
                    .HasForeignKey(i => i.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.SubscriptionPayments)
                    .WithOne(p => p.Company)
                    .HasForeignKey(p => p.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.CompanySubscriptions)
                    .WithOne(s => s.Company)
                    .HasForeignKey(s => s.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SubscriptionPlan>(entity =>
            {
                entity.HasIndex(p => p.Name).IsUnique();
                entity.HasData(
                    new SubscriptionPlan
                    {
                        Id = 1,
                        Name = Models.SubscriptionPlans.Starter,
                        Description = "Starter plan for small property portfolios.",
                        Price = 99m,
                        Currency = "MYR",
                        BillingCycle = "Monthly",
                        MaxProperties = 1,
                        MaxManagers = 2,
                        MaxTenants = 25,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new SubscriptionPlan
                    {
                        Id = 2,
                        Name = Models.SubscriptionPlans.Professional,
                        Description = "Professional plan for growing property teams.",
                        Price = 299m,
                        Currency = "MYR",
                        BillingCycle = "Monthly",
                        MaxProperties = 5,
                        MaxManagers = 10,
                        MaxTenants = 150,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new SubscriptionPlan
                    {
                        Id = 3,
                        Name = Models.SubscriptionPlans.Enterprise,
                        Description = "Enterprise plan for large portfolios.",
                        Price = 599m,
                        Currency = "MYR",
                        BillingCycle = "Monthly",
                        MaxProperties = 20,
                        MaxManagers = int.MaxValue,
                        MaxTenants = 500,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
                    });
            });

            builder.Entity<CompanySubscription>(entity =>
            {
                entity.HasIndex(s => s.CompanyId);
                entity.HasIndex(s => s.SubscriptionPlanId);
                entity.HasIndex(s => s.Status);

                entity.HasOne(s => s.SubscriptionPlan)
                    .WithMany(p => p.CompanySubscriptions)
                    .HasForeignKey(s => s.SubscriptionPlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CompanySubscriptionEvent>(entity =>
            {
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => e.ProviderEventId).IsUnique();
                entity.HasIndex(e => e.CreatedAt);
            });

            builder.Entity<CompanyInvoice>(entity =>
            {
                entity.HasIndex(i => i.CompanyId);
                entity.HasIndex(i => i.ProviderInvoiceId).IsUnique();
            });

            builder.Entity<SubscriptionPayment>(entity =>
            {
                entity.HasIndex(p => p.CompanyId);
                entity.HasIndex(p => p.CompanySubscriptionId);
                entity.HasIndex(p => p.ProviderInvoiceId).IsUnique();
                entity.HasIndex(p => p.TransactionReference).IsUnique();
                entity.HasIndex(p => p.PaymentStatus);

                entity.HasOne(p => p.CompanySubscription)
                    .WithMany(s => s.Payments)
                    .HasForeignKey(p => p.CompanySubscriptionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Property>()
                .HasMany(p => p.Units)
                .WithOne(u => u.Property)
                .HasForeignKey(u => u.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Property>()
                .HasMany(p => p.MaintenanceRequests)
                .WithOne(m => m.Property)
                .HasForeignKey(m => m.PropertyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Unit>()
                .HasMany(u => u.MaintenanceRequests)
                .WithOne(m => m.Unit)
                .HasForeignKey(m => m.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Identity maps "Roles" + "Users" from the design doc to AspNetRoles / AspNetUsers — no duplicate tables.
            builder.Entity<TenantProfile>(entity =>
            {
                entity.HasIndex(t => t.UserId).IsUnique();
                entity.HasIndex(t => t.UnitId).IsUnique();

                entity.HasOne(t => t.User)
                    .WithOne(u => u.TenantProfile)
                    .HasForeignKey<TenantProfile>(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Unit)
                    .WithOne(u => u.TenantProfile)
                    .HasForeignKey<TenantProfile>(t => t.UnitId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(t => t.MaintenanceRequests)
                    .WithOne(m => m.Tenant)
                    .HasForeignKey(m => m.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TechnicianProfile>(entity =>
            {
                entity.HasIndex(t => t.UserId).IsUnique();

                entity.HasOne(t => t.User)
                    .WithOne(u => u.TechnicianProfile)
                    .HasForeignKey<TechnicianProfile>(t => t.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(t => t.UploadedDocuments)
                    .WithOne(d => d.Technician)
                    .HasForeignKey(d => d.TechnicianId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AdministratorProfile>(entity =>
            {
                entity.HasIndex(a => a.UserId).IsUnique();

                entity.HasOne(a => a.User)
                    .WithOne(u => u.AdministratorProfile)
                    .HasForeignKey<AdministratorProfile>(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ManagerProfile>(entity =>
            {
                entity.HasIndex(m => m.UserId).IsUnique();
                entity.HasIndex(m => m.PropertyId);

                entity.HasOne(m => m.User)
                    .WithOne(u => u.ManagerProfile)
                    .HasForeignKey<ManagerProfile>(m => m.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.AssignedProperty)
                    .WithMany(p => p.Managers)
                    .HasForeignKey(m => m.PropertyId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RequestImage>(entity =>
            {
                entity.HasOne(r => r.UploadedByUser)
                    .WithMany(u => u.UploadedRequestImages)
                    .HasForeignKey(r => r.UploadedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RequestStatusHistory>(entity =>
            {
                entity.HasOne(h => h.ChangedByUser)
                    .WithMany(u => u.RequestStatusChanges)
                    .HasForeignKey(h => h.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // MaintenanceRequests 1 — 0..1 Assignment (FK on Assignment)
            builder.Entity<Assignment>(entity =>
            {
                entity.HasIndex(a => a.RequestId).IsUnique();

                entity.HasOne(a => a.Request)
                    .WithOne(r => r.Assignment)
                    .HasForeignKey<Assignment>(a => a.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Technician)
                    .WithMany(t => t.Assignments)
                    .HasForeignKey(a => a.TechnicianId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.AssignedByManager)
                    .WithMany(u => u.ManagerAssignments)
                    .HasForeignKey(a => a.AssignedByManagerId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Assignments 1 — 0..1 ServicePayment
                entity.HasOne(a => a.ServicePayment)
                    .WithOne(p => p.Assignment)
                    .HasForeignKey<ServicePayment>(p => p.AssignmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // MaintenanceRequests 1 — 0..1 ServicePayment
            builder.Entity<ServicePayment>(entity =>
            {
                entity.HasIndex(p => p.RequestId).IsUnique();

                entity.HasOne(p => p.Request)
                    .WithOne(r => r.ServicePayment)
                    .HasForeignKey<ServicePayment>(p => p.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Manager)
                    .WithMany(u => u.RecordedServicePayments)
                    .HasForeignKey(p => p.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Technician)
                    .WithMany(t => t.ServicePayments)
                    .HasForeignKey(p => p.TechnicianId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TechnicianPayment>(entity =>
            {
                entity.HasIndex(p => p.MaintenanceRequestId).IsUnique();
                entity.HasIndex(p => p.CompanyId);
                entity.HasIndex(p => p.PaidAt);
                entity.HasIndex(p => p.TransactionReference).IsUnique();
                entity.HasIndex(p => p.ProviderInvoiceId).IsUnique();

                entity.HasOne(p => p.MaintenanceRequest)
                    .WithOne(r => r.TechnicianPayment)
                    .HasForeignKey<TechnicianPayment>(p => p.MaintenanceRequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Manager)
                    .WithMany(u => u.RecordedTechnicianPayments)
                    .HasForeignKey(p => p.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Technician)
                    .WithMany(t => t.TechnicianPayments)
                    .HasForeignKey(p => p.TechnicianId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Company)
                    .WithMany()
                    .HasForeignKey(p => p.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TechnicianPayout>(entity =>
            {
                entity.HasIndex(p => p.TechnicianPaymentId).IsUnique();
                entity.HasIndex(p => p.TechnicianId);
                entity.HasIndex(p => p.PayoutStatus);

                entity.HasOne(p => p.TechnicianPayment)
                    .WithOne(p => p.Payout)
                    .HasForeignKey<TechnicianPayout>(p => p.TechnicianPaymentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Technician)
                    .WithMany()
                    .HasForeignKey(p => p.TechnicianId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<RequestFeedback>(entity =>
            {
                entity.HasIndex(f => f.RequestId).IsUnique();

                entity.HasOne(f => f.Request)
                    .WithOne(r => r.RequestFeedback)
                    .HasForeignKey<RequestFeedback>(f => f.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Tenant)
                    .WithMany(t => t.RequestFeedbacks)
                    .HasForeignKey(f => f.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.Technician)
                    .WithMany(t => t.RequestFeedbacks)
                    .HasForeignKey(f => f.TechnicianId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.Request)
                    .WithMany(r => r.Notifications)
                    .HasForeignKey(n => n.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AuditLog>(entity =>
            {
                entity.HasOne(a => a.User)
                    .WithMany(u => u.AuditLogs)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Conversation>(entity =>
            {
                entity.HasIndex(c => new { c.RequestId, c.Type }).IsUnique();

                entity.HasOne(c => c.Request)
                    .WithMany(r => r.Conversations)
                    .HasForeignKey(c => c.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ConversationParticipant>(entity =>
            {
                entity.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique();

                entity.HasOne(p => p.Conversation)
                    .WithMany(c => c.Participants)
                    .HasForeignKey(p => p.ConversationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.User)
                    .WithMany(u => u.ConversationParticipants)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Conversation)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(m => m.ConversationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Sender)
                    .WithMany(u => u.SentMessages)
                    .HasForeignKey(m => m.SenderUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
