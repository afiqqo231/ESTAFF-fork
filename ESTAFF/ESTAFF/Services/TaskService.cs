using System;
using System.Data.Entity;
using System.Linq;
using ESTAFF.Models.Data;

namespace ESTAFF.Services
{
    public class TaskService
    {
        private readonly ApplicationDbContext _db;

        public TaskService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Auto-flag overdue tasks
        public void UpdateOverdueTasks()
        {
            var today = DateTime.Today;

            var overdueTasks = _db.TaskItems
                .Where(t => t.Status != TaskStatus.Complete
                         && t.Status != TaskStatus.Overdue
                         && DbFunctions.TruncateTime(t.DueDate) < today)
                .ToList();

            if (!overdueTasks.Any()) return;

            foreach (var task in overdueTasks)
            {
                var oldStatus = task.Status.ToString();
                task.Status           = TaskStatus.Overdue;
                task.LastModifiedDate = DateTime.Now;

                _db.TaskHistories.Add(new TaskHistory
                {
                    TaskId          = task.TaskId,
                    Action          = "StatusChanged",
                    OldValue        = oldStatus,
                    NewValue        = TaskStatus.Overdue.ToString(),
                    ChangedByUserId = task.CreatedByUserId,
                    ChangedDate     = DateTime.Now
                });
            }

            _db.SaveChanges();
        }

        // Log task history
        public void LogHistory(int taskId, string action,
            string oldValue, string newValue, string changedByUserId)
        {
            _db.TaskHistories.Add(new TaskHistory
            {
                TaskId          = taskId,
                Action          = action,
                OldValue        = oldValue,
                NewValue        = newValue,
                ChangedByUserId = changedByUserId,
                ChangedDate     = DateTime.Now
            });

            _db.SaveChanges();
        }
    }
}