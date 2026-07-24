using System;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ESTAFF.Models.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public virtual DbSet<TaskItem> TaskItems { get; set; }
        public virtual DbSet<TaskHistory> TaskHistories { get; set; }
        public virtual DbSet<Report> Reports { get; set; }
        public virtual DbSet<ReportApproval> ReportApprovals { get; set; }

        // Read-only projections of tables owned/created by EHS_PORTAL's CLIP module (same CLIP schema/DB).
        // Never write through these - see PreventClipReadOnlyWrites().
        public virtual DbSet<COF> COFs { get; set; }
        public virtual DbSet<Plant> Plants { get; set; }
        public virtual DbSet<UserPlant> UserPlants { get; set; }
        public virtual DbSet<PlantMonitoring> PlantMonitoring { get; set; }
        public virtual DbSet<Monitoring> Monitoring {get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", schemaName: "CLIP");
            modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserRole>().ToTable("AspNetUserRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("AspNetUserClaims", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("AspNetUserLogins", schemaName: "CLIP");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.IsAdmin)
                .HasColumnName("IsAdmin");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.ProfilePicturePath)
                .HasColumnName("ProfilePicturePath");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.HireDate)
                .HasColumnName("HireDate");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.CreatedDate)
                .HasColumnName("CreatedDate");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.LastModifiedDate)
                .HasColumnName("LastModifiedDate");

            // ESTAFF-owned tables, in ESTAFF schema, FK'ing to CLIP.AspNetUsers via ApplicationUser
            modelBuilder.Entity<Report>().ToTable("Reports", "ESTAFF");
            modelBuilder.Entity<ReportApproval>().ToTable("ReportApprovals", "ESTAFF");
            modelBuilder.Entity<Staff>().ToTable("Staffs", "ESTAFF");
            modelBuilder.Entity<TaskItem>().ToTable("TaskItems", "ESTAFF");
            modelBuilder.Entity<TaskHistory>().ToTable("TaskHistories", "ESTAFF");

            // TaskItem relationships
            modelBuilder.Entity<Staff>()
                .HasRequired(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Staff>()
                .HasRequired(s => s.Manager)
                .WithMany()
                .HasForeignKey(s => s.ManagerId)
                .WillCascadeOnDelete(false);
            
            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .WillCascadeOnDelete(false);


            modelBuilder.Entity<TaskHistory>()
                .HasRequired(h => h.Task)
                .WithMany(t => t.Histories)
                .HasForeignKey(h => h.TaskId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<TaskHistory>()
                .HasRequired(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
                .HasRequired(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .WillCascadeOnDelete(false);

            // Read-only CLIP tables (owned by EHS_PORTAL - do not CreateTable/AddColumn for these in migrations)
            modelBuilder.Entity<COF>().ToTable("CertificateOfFitness", "CLIP");
            modelBuilder.Entity<Plant>().ToTable("Plants", "CLIP");
            modelBuilder.Entity<UserPlant>().ToTable("UserPlants", "CLIP");
            modelBuilder.Entity<PlantMonitoring>().ToTable("PlantMonitorings", "CLIP");
            modelBuilder.Entity<Monitoring>().ToTable("Monitorings", "CLIP");

            modelBuilder.Entity<COF>()
                .HasRequired(c => c.Plant)
                .WithMany()
                .HasForeignKey(c => c.PlantId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<UserPlant>()
                .HasRequired(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<UserPlant>()
                .HasRequired(up => up.Plant)
                .WithMany(p => p.UserPlants)
                .HasForeignKey(up => up.PlantId)
                .WillCascadeOnDelete(false);
            
            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(up => up.Plant)
                .WithMany()
                .HasForeignKey(up => up.PlantID)
                .WillCascadeOnDelete(false);
            
            modelBuilder.Entity<PlantMonitoring>()
                .HasRequired(up => up.Monitoring)
                .WithMany()
                .HasForeignKey(up => up.MonitoringID)
                .WillCascadeOnDelete(false);
        }

        public override int SaveChanges()
        {
            PreventClipReadOnlyWrites();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            PreventClipReadOnlyWrites();
            return base.SaveChangesAsync(cancellationToken);
        }

        // COF/Plant/UserPlant mirror tables that EHS_PORTAL's CLIP module owns and writes to.
        // Blocking writes here at the SaveChanges level guards against silently corrupting
        // EHS_PORTAL's data if a future ESTAFF change accidentally Add/Update/Removes one.
        private void PreventClipReadOnlyWrites()
        {
            var hasClipWrites = ChangeTracker.Entries()
                .Any(e => e.State != EntityState.Unchanged &&
                          (e.Entity is COF || e.Entity is Plant || e.Entity is UserPlant));

            if (hasClipWrites)
            {
                throw new InvalidOperationException(
                    "COF, Plant, and UserPlant are read-only projections of EHS_PORTAL's CLIP schema and cannot be written from ESTAFF.");
            }
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}