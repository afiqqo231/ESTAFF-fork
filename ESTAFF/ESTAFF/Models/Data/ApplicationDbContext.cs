using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ESTAFF.Models.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext() : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public virtual DbSet<TaskItem> Tasks { get; set; }
        public virtual DbSet<TaskHistory> TaskHistories { get; set; }
        public virtual DbSet<Report> Reports { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

                // TaskItem relationships
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