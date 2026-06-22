using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ESTAFF.Models.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public virtual DbSet<Staff> Staffs { get; set; }
        public virtual DbSet<TaskItem> Tasks { get; set; }
        public virtual DbSet<TaskHistory> TaskHistories { get; set; }
        public virtual DbSet<Report> Reports { get; set; }
        public virtual DbSet<ReportApproval> Reportapprovals { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Staff relationships
            modelBuilder.Entity<Staff>()
                .HasRequired(e => e.User)
                .WithMany(u => u.ManagedStaffs)
                .HasForeignKey(e => e.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Staff>()
                .HasRequired(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .WillCascadeOnDelete(false);

                // TaskItem relationships
            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.AssignedToUser)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TaskItem>()
                .HasRequired(t => t.CreatedByUser)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.CreatedByUserId)
                .WillCascadeOnDelete(false);

            // Report relationships
            modelBuilder.Entity<Report>()
                .HasRequired(r => r.Staff)
                .WithMany()
                .HasForeignKey(r => r.StaffId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<Report>()
                .HasOptional(r => r.SubmittedByUser)
                .WithMany(u => u.SubmittedReports)
                .HasForeignKey(r => r.SubmittedByUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
                .HasOptional(r => r.ApprovedByUser)
                .WithMany(u => u.ApprovedReports)
                .HasForeignKey(r => r.ApprovedByUserId)
                .WillCascadeOnDelete(false);
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}