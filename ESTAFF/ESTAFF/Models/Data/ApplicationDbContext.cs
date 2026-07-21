using System.Data.Entity;
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
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}