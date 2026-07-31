using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    public class TaskService
    {
        // TaskHistory.Action value used for every status transition. Status
        // changes are logged under their own action so the newest remark for a
        // task can be found without parsing free text.
        public const string StatusChangedAction = "StatusChanged";

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
                    Action          = StatusChangedAction,
                    OldValue        = oldStatus,
                    NewValue        = TaskStatus.Overdue.ToString(),
                    Remark          = "Automatically flagged overdue - the due date passed.",
                    ChangedByUserId = task.CreatedByUserId,
                    ChangedDate     = DateTime.Now
                });
            }

            _db.SaveChanges();
        }

        // Log task history
        public void LogHistory(int taskId, string action,
            string oldValue, string newValue, string changedByUserId,
            string remark = null)
        {
            _db.TaskHistories.Add(new TaskHistory
            {
                TaskId          = taskId,
                Action          = action,
                OldValue        = oldValue,
                NewValue        = newValue,
                Remark          = TrimRemark(remark),
                ChangedByUserId = changedByUserId,
                ChangedDate     = DateTime.Now
            });

            _db.SaveChanges();
        }

        // Log a status transition with the optional remark the user supplied.
        public void LogStatusChange(int taskId, TaskStatus oldStatus,
            TaskStatus newStatus, string changedByUserId, string remark)
        {
            LogHistory(taskId, StatusChangedAction,
                oldStatus.ToString(), newStatus.ToString(),
                changedByUserId, remark);
        }

        // ══════════════════════════════════════════
        // LATEST STATUS REMARK
        // ══════════════════════════════════════════

        public StatusRemarkViewModel GetLatestStatusRemark(int taskId)
        {
            var entry = _db.TaskHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.TaskId == taskId
                         && h.Action == StatusChangedAction)
                .OrderByDescending(h => h.ChangedDate)
                .ThenByDescending(h => h.HistoryId)
                .FirstOrDefault();

            return MapRemark(entry);
        }

        // Newest status change per task, keyed by TaskId. One query for the whole
        // page rather than one per task.
        public Dictionary<int, StatusRemarkViewModel> GetLatestStatusRemarks(
            IEnumerable<int> taskIds)
        {
            var ids = (taskIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (!ids.Any()) return new Dictionary<int, StatusRemarkViewModel>();

            return _db.TaskHistories
                .Include(h => h.ChangedByUser)
                .Where(h => ids.Contains(h.TaskId)
                         && h.Action == StatusChangedAction)
                .ToList()
                .GroupBy(h => h.TaskId)
                .ToDictionary(
                    g => g.Key,
                    g => MapRemark(g
                        .OrderByDescending(h => h.ChangedDate)
                        .ThenByDescending(h => h.HistoryId)
                        .First()));
        }

        private static StatusRemarkViewModel MapRemark(TaskHistory entry)
        {
            if (entry == null) return null;

            return new StatusRemarkViewModel
            {
                TaskId        = entry.TaskId,
                FromStatus    = ParseStatus(entry.OldValue),
                ToStatus      = ParseStatus(entry.NewValue),
                Remark        = entry.Remark,
                ChangedByName = entry.ChangedByUser != null
                                    ? entry.ChangedByUser.UserName
                                    : "-",
                ChangedDate   = entry.ChangedDate
            };
        }

        private static TaskStatus? ParseStatus(string value)
        {
            TaskStatus parsed;
            return Enum.TryParse(value, out parsed) ? parsed : (TaskStatus?)null;
        }

        private static string TrimRemark(string remark)
        {
            if (string.IsNullOrWhiteSpace(remark)) return null;

            remark = remark.Trim();
            return remark.Length > 500 ? remark.Substring(0, 500) : remark;
        }
    }
}
