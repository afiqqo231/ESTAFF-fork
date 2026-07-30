using System;
using System.Collections.Generic;
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

        public List<COF> GetCOF(int plantId)
        {
            return _db.COFs
                .Where(c => c.PlantId == plantId)
                .ToList();
        }

        public List<COF> GetCOFsForPlants(IEnumerable<int> plantIds)
        {
            return _db.COFs
                .Where(c => plantIds.Contains(c.PlantId))
                .ToList();
        }

        public List<PlantMonitoring> GetPlantMonitoringList(int plantId)
        {
            return _db.PlantMonitoring
                .Where(m => m.PlantID == plantId)
                .ToList();
        }

        public List<COF> GetCOFList(int plantId)
        {
            return _db.COFs
                .Where(c => c.PlantId == plantId)
                .ToList();
        }
        
        public List<TaskClassification> GetTaskClassification()
        {
            return _db.TaskClassifications.ToList();
        }

        public List<TaskList> GetTaskList(int classificationId)
        {
            return _db.TaskLists.Where(t => t.TaskClassificationId == classificationId).ToList();
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