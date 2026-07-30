using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ESTAFF.Models.Data
{
    // ESTAFF's own tables, plus the shared Identity tables it logs in against.
    //
    // The CLIP schema is deliberately absent here — EHS_PORTAL owns it, and
    // ESTAFF reads it through ClipDbContext, which has no initializer and so
    // can never migrate it. Keep it that way: mapping a CLIP table on this
    // context puts EHS_PORTAL's data within reach of ESTAFF's migrations.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        static ApplicationDbContext()
        {
            // No automatic schema management, for anything that opens this
            // context — the web app, a console tool, a scheduled job.
            //
            // EF's default initializer would compare the model against the
            // database and offer to reconcile it. ESTAFF's entities map only the
            // columns it reads, and this database also hosts EHS_PORTAL's CLIP,
            // CORD and FETS schemas, so reconciling would drop live columns
            // another application depends on. Schema changes are applied
            // deliberately: the scripts in DATABASE/, or Update-Database from the
            // Package Manager Console (which drives the migrator directly and is
            // unaffected by this).
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public virtual DbSet<TaskItem> TaskItems { get; set; }
        public virtual DbSet<TaskHistory> TaskHistories { get; set; }
        public virtual DbSet<TaskClassification> TaskClassifications { get; set; }
        public virtual DbSet<TaskList> TaskLists { get; set; }
        public virtual DbSet<Report> Reports { get; set; }
        public virtual DbSet<ReportApproval> ReportApprovals { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Identity lives in the CLIP schema: ESTAFF and EHS_PORTAL share one
            // set of accounts. ESTAFF reads and updates users, but never the rest
            // of CLIP.
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", schemaName: "CLIP");
            modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserRole>().ToTable("AspNetUserRoles", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("AspNetUserClaims", schemaName: "CLIP");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("AspNetUserLogins", schemaName: "CLIP");

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.IsAdmin)
                .HasColumnName("IsAdmin");

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
            modelBuilder.Entity<TaskClassification>().ToTable("TaskClassifications", "ESTAFF");
            modelBuilder.Entity<TaskList>().ToTable("TaskLists", "ESTAFF");

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

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.TaskClassification)
                .WithMany()
                .HasForeignKey(t => t.TaskClassificationId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasOptional(t => t.TaskList)
                .WithMany()
                .HasForeignKey(t => t.TaskListId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskList>()
                .HasRequired(l => l.TaskClassification)
                .WithMany(c => c.TaskLists)
                .HasForeignKey(l => l.TaskClassificationId)
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
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}
