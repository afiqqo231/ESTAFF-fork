using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    // Read-only access to the CLIP schema that EHS_PORTAL owns, plus the rules
    // that connect an ESTAFF task to a CLIP record.
    //
    // A task is CLIP work when its TaskClassification is named "CLIP". Which
    // CLIP table SubTaskId points at is decided by the task's TaskList:
    //   "Certificate Of FItness" -> CLIP.CertificateOfFitness
    //   "Plant Monitoring"       -> CLIP.PlantMonitoring
    //
    // The expiry rules below deliberately mirror EHS_PORTAL so the two apps
    // never disagree about whether an item is expiring:
    //   Areas/CLIP/Controllers/CertificateOfFitnessController.CalculateStatus  (60 days)
    //   Areas/CLIP/Models/PlantMonitoring.CalculateExpStatus                   (90 days)
    // CLIP stores computed Status/ExpStatus columns, but they only refresh when
    // EHS_PORTAL saves the record, so they go stale. We recompute from the
    // expiry date instead.
    public class ClipService : IDisposable
    {
        public const int CofExpiringSoonDays = 60;
        public const int MonitoringExpiringSoonDays = 90;

        private readonly ApplicationDbContext _db;
        private readonly ClipDbContext _clip;
        private readonly bool _ownsClipContext;

        public ClipService(ApplicationDbContext db)
        {
            _db = db;
            _clip = new ClipDbContext();
            _ownsClipContext = true;
        }

        public ClipService(ApplicationDbContext db, ClipDbContext clip)
        {
            _db = db;
            _clip = clip;
            _ownsClipContext = false;
        }

        // ══════════════════════════════════════════
        // STATUS RULES (mirrored from EHS_PORTAL/CLIP)
        // ══════════════════════════════════════════

        public static string CalculateCofStatus(DateTime expiryDate)
        {
            var today = DateTime.Today;

            if (today > expiryDate.Date) return "Expired";
            if (today >= expiryDate.Date.AddDays(-CofExpiringSoonDays))
                return "Expiring Soon";
            return "Active";
        }

        public static string CalculateMonitoringExpStatus(DateTime? expDate)
        {
            if (!expDate.HasValue) return "No Expiry";
            if (expDate.Value < DateTime.Now) return "Expired";
            if (expDate.Value < DateTime.Now.AddDays(MonitoringExpiringSoonDays))
                return "Expiring Soon";
            return "Active";
        }

        public static ClipUrgency ToUrgency(string expiryStatus)
        {
            switch (expiryStatus)
            {
                case "Expired":       return ClipUrgency.Expired;
                case "Expiring Soon": return ClipUrgency.ExpiringSoon;
                case "Active":        return ClipUrgency.Active;
                default:              return ClipUrgency.None;
            }
        }

        // ══════════════════════════════════════════
        // CLASSIFICATION / TASK LIST RESOLUTION
        // ══════════════════════════════════════════

        public TaskClassification GetClipClassification()
        {
            return _db.TaskClassifications
                .FirstOrDefault(c => c.Name == TaskClassification.ClipName);
        }

        public bool IsClipClassification(int? taskClassificationId)
        {
            if (!taskClassificationId.HasValue) return false;

            var clip = GetClipClassification();
            return clip != null
                && clip.TaskClassificationId == taskClassificationId.Value;
        }

        // The CLIP classification's task lists, tagged with the CLIP table each
        // one refers to. Matched on the name rather than a hard-coded id so the
        // rows stay editable; the match is loose enough to survive the
        // "Certificate Of FItness" typo in the seeded data.
        public Dictionary<int, ClipItemKind> GetClipTaskListKinds()
        {
            var result = new Dictionary<int, ClipItemKind>();

            var clip = GetClipClassification();
            if (clip == null) return result;

            var lists = _db.TaskLists
                .Where(l => l.TaskClassificationId == clip.TaskClassificationId)
                .ToList();

            foreach (var list in lists)
            {
                var kind = ClassifyTaskListName(list.Name);
                if (kind.HasValue) result[list.TaskListId] = kind.Value;
            }

            return result;
        }

        public static ClipItemKind? ClassifyTaskListName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var n = name.ToLowerInvariant();

            if (n.Contains("fitness") || n.Contains("cof"))
                return ClipItemKind.COF;

            if (n.Contains("monitoring"))
                return ClipItemKind.PlantMonitoring;

            return null;
        }

        // The task list id to store for a given CLIP item kind, so picking an
        // item in the CLIP dropdown also sets the task's sub-type.
        public int? GetTaskListIdFor(ClipItemKind kind)
        {
            foreach (var pair in GetClipTaskListKinds())
                if (pair.Value == kind) return pair.Key;

            return null;
        }

        // ══════════════════════════════════════════
        // PLANT ACCESS
        // ══════════════════════════════════════════

        public List<int> GetPlantIdsForUser(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new List<int>();

            return _clip.UserPlants
                .Where(up => up.UserId == userId)
                .Select(up => up.PlantId)
                .Distinct()
                .ToList();
        }

        // ══════════════════════════════════════════
        // ITEM LOOKUPS
        // ══════════════════════════════════════════

        // Every CLIP record the user can act on, nearest-expiry first
        // (already-expired items lead, then soonest, undated items last).
        public List<ClipItemViewModel> GetItemsForUser(string userId)
        {
            return GetItemsForPlants(GetPlantIdsForUser(userId));
        }

        public List<ClipItemViewModel> GetItemsForPlants(IEnumerable<int> plantIds)
        {
            var ids = (plantIds ?? Enumerable.Empty<int>()).Distinct().ToList();
            if (!ids.Any()) return new List<ClipItemViewModel>();

            var items = new List<ClipItemViewModel>();

            items.AddRange(_clip.COFs
                .Include(c => c.Plant)
                .Where(c => ids.Contains(c.PlantId))
                .ToList()
                .Select(MapCof));

            items.AddRange(_clip.PlantMonitoring
                .Include(pm => pm.Plant)
                .Include(pm => pm.Monitoring)
                .Where(pm => ids.Contains(pm.PlantID))
                .ToList()
                .Select(MapMonitoring));

            return SortByExpiry(items);
        }

        // Nearest expiry first: expired items are the most urgent, undated last.
        public static List<ClipItemViewModel> SortByExpiry(
            IEnumerable<ClipItemViewModel> items)
        {
            return items
                .OrderBy(i => i.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(i => i.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(i => i.Title)
                .ToList();
        }

        public ClipItemViewModel GetCofItem(int cofId)
        {
            var cof = _clip.COFs
                .Include(c => c.Plant)
                .FirstOrDefault(c => c.Id == cofId);
            return cof == null ? null : MapCof(cof);
        }

        public ClipItemViewModel GetMonitoringItem(int plantMonitoringId)
        {
            var pm = _clip.PlantMonitoring
                .Include(m => m.Plant)
                .Include(m => m.Monitoring)
                .FirstOrDefault(m => m.Id == plantMonitoringId);
            return pm == null ? null : MapMonitoring(pm);
        }

        // Batch lookup for list pages so rendering N tasks stays at two queries
        // rather than N. Keyed by TaskId.
        public Dictionary<int, ClipItemViewModel> GetItemsForTasks(
            IEnumerable<TaskItem> tasks)
        {
            var list = (tasks ?? Enumerable.Empty<TaskItem>()).ToList();
            var result = new Dictionary<int, ClipItemViewModel>();

            var kinds = GetClipTaskListKinds();
            if (!kinds.Any()) return result;

            // Pair each task with the CLIP table its task list points at.
            var linked = list
                .Where(t => t.SubTaskId.HasValue
                         && t.TaskListId.HasValue
                         && kinds.ContainsKey(t.TaskListId.Value))
                .Select(t => new
                {
                    Task = t,
                    Kind = kinds[t.TaskListId.Value],
                    Id   = t.SubTaskId.Value
                })
                .ToList();

            if (!linked.Any()) return result;

            var cofIds = linked.Where(x => x.Kind == ClipItemKind.COF)
                .Select(x => x.Id).Distinct().ToList();
            var pmIds = linked.Where(x => x.Kind == ClipItemKind.PlantMonitoring)
                .Select(x => x.Id).Distinct().ToList();

            var cofs = cofIds.Any()
                ? _clip.COFs
                    .Include(c => c.Plant)
                    .Where(c => cofIds.Contains(c.Id))
                    .ToList().Select(MapCof)
                    .ToDictionary(i => i.Id)
                : new Dictionary<int, ClipItemViewModel>();

            var monitorings = pmIds.Any()
                ? _clip.PlantMonitoring
                    .Include(m => m.Plant)
                    .Include(m => m.Monitoring)
                    .Where(m => pmIds.Contains(m.Id))
                    .ToList().Select(MapMonitoring)
                    .ToDictionary(i => i.Id)
                : new Dictionary<int, ClipItemViewModel>();

            foreach (var entry in linked)
            {
                ClipItemViewModel item = null;

                if (entry.Kind == ClipItemKind.COF)
                    cofs.TryGetValue(entry.Id, out item);
                else
                    monitorings.TryGetValue(entry.Id, out item);

                if (item != null) result[entry.Task.TaskId] = item;
            }

            return result;
        }

        // ══════════════════════════════════════════
        // PICKER KEY  ("COF:14" / "PM:3")
        // ══════════════════════════════════════════

        public static bool TryParseKey(string key, out ClipItemKind kind, out int id)
        {
            kind = ClipItemKind.COF;
            id = 0;

            if (string.IsNullOrWhiteSpace(key)) return false;

            var parts = key.Split(':');
            if (parts.Length != 2) return false;

            if (!int.TryParse(parts[1], out id) || id <= 0) return false;

            switch (parts[0].Trim().ToUpperInvariant())
            {
                case "COF": kind = ClipItemKind.COF; return true;
                case "PM":  kind = ClipItemKind.PlantMonitoring; return true;
                default:    return false;
            }
        }

        public static string BuildKey(ClipItemKind kind, int? subTaskId)
        {
            if (!subTaskId.HasValue) return null;

            return (kind == ClipItemKind.COF ? "COF:" : "PM:") + subTaskId.Value;
        }

        // The picker key for a task that is already linked, or null.
        public string BuildKeyForTask(TaskItem task)
        {
            if (task == null || !task.SubTaskId.HasValue
                || !task.TaskListId.HasValue) return null;

            var kinds = GetClipTaskListKinds();
            ClipItemKind kind;

            return kinds.TryGetValue(task.TaskListId.Value, out kind)
                ? BuildKey(kind, task.SubTaskId)
                : null;
        }

        // Resolves a posted picker key onto a task, setting both the task list
        // and the sub-task id. Rejects anything outside the plants the assignee
        // is allowed to see, so a hand-crafted post cannot reach another plant.
        public bool ApplyKeyToTask(TaskItem task, string key, string ownerUserId)
        {
            task.SubTaskId = null;
            task.TaskListId = null;

            ClipItemKind kind;
            int id;
            if (!TryParseKey(key, out kind, out id)) return false;

            var taskListId = GetTaskListIdFor(kind);
            if (!taskListId.HasValue) return false;

            var allowedPlantIds = GetPlantIdsForUser(ownerUserId);

            if (kind == ClipItemKind.COF)
            {
                var cof = _clip.COFs.FirstOrDefault(c => c.Id == id);
                if (cof == null || !allowedPlantIds.Contains(cof.PlantId))
                    return false;
            }
            else
            {
                var pm = _clip.PlantMonitoring.FirstOrDefault(m => m.Id == id);
                if (pm == null || !allowedPlantIds.Contains(pm.PlantID))
                    return false;
            }

            task.TaskListId = taskListId;
            task.SubTaskId = id;
            return true;
        }

        // Sets TaskListId/SubTaskId from what a task form posted. For CLIP the
        // picked record decides both; every other classification picks a task
        // type directly and links no record. False only when the CLIP item is
        // not one the task's owner may use — the caller reports that.
        //
        // Shared by the admin and employee forms so the rule cannot drift.
        public bool TryApplyClassificationLink(TaskItem task,
            int? classificationId, int? taskListId, string clipItemKey,
            string ownerUserId, bool isClip)
        {
            if (isClip)
                return ApplyKeyToTask(task, clipItemKey, ownerUserId);

            task.SubTaskId = null;
            task.TaskListId = null;

            // Only accept a task type that actually belongs to the chosen
            // classification, so a stale or hand-edited post cannot cross them.
            if (taskListId.HasValue && classificationId.HasValue)
            {
                var belongs = _db.TaskLists.Any(l =>
                    l.TaskListId == taskListId.Value
                    && l.TaskClassificationId == classificationId.Value);

                if (belongs) task.TaskListId = taskListId;
            }

            return true;
        }

        // "CLIP / Plant Monitoring / #12" — a stable string for the audit trail.
        public string DescribeClassification(TaskItem task)
        {
            var classification = _db.TaskClassifications
                .FirstOrDefault(c =>
                    c.TaskClassificationId == task.TaskClassificationId);

            var parts = new List<string>
            {
                classification?.Name ?? task.TaskClassificationId.ToString()
            };

            if (task.TaskListId.HasValue)
            {
                var list = _db.TaskLists
                    .FirstOrDefault(l => l.TaskListId == task.TaskListId.Value);
                parts.Add(list?.Name ?? ("#" + task.TaskListId.Value));
            }

            if (task.SubTaskId.HasValue) parts.Add("#" + task.SubTaskId.Value);

            return string.Join(" / ", parts);
        }

        // ══════════════════════════════════════════
        // MAPPING
        // ══════════════════════════════════════════

        private static ClipItemViewModel MapCof(COF cof)
        {
            var status = CalculateCofStatus(cof.ExpiryDate);

            return new ClipItemViewModel
            {
                Kind         = ClipItemKind.COF,
                Id           = cof.Id,
                PlantId      = cof.PlantId,
                PlantName    = cof.Plant != null ? cof.Plant.PlantName : null,
                Title        = cof.RegistrationNo,
                Subtitle     = !string.IsNullOrWhiteSpace(cof.MachineName)
                                   ? cof.MachineName
                                   : cof.Location,
                ExpiryDate   = cof.ExpiryDate,
                ExpiryStatus = status,
                Urgency      = ToUrgency(status)
            };
        }

        private static ClipItemViewModel MapMonitoring(PlantMonitoring pm)
        {
            var status = CalculateMonitoringExpStatus(pm.ExpDate);

            return new ClipItemViewModel
            {
                Kind          = ClipItemKind.PlantMonitoring,
                Id            = pm.Id,
                PlantId       = pm.PlantID,
                PlantName     = pm.Plant != null ? pm.Plant.PlantName : null,
                Title         = pm.Monitoring != null
                                    ? pm.Monitoring.MonitoringName
                                    : "Monitoring #" + pm.MonitoringID,
                Subtitle      = !string.IsNullOrWhiteSpace(pm.Area)
                                    ? pm.Area
                                    : (pm.Monitoring != null
                                        ? pm.Monitoring.MonitoringCategory
                                        : null),
                ExpiryDate    = pm.ExpDate,
                ExpiryStatus  = status,
                ProcessStatus = CalculateProcStatus(pm),
                Urgency       = ToUrgency(status)
            };
        }

        // Mirrors PlantMonitoring.CalculateProcStatus in EHS_PORTAL. Recomputed
        // rather than read from ProcStatus so it cannot drift out of date.
        private static string CalculateProcStatus(PlantMonitoring pm)
        {
            if (pm.WorkCompleteDate.HasValue) return "Completed";
            if (pm.WorkDate.HasValue)         return "Work In Progress";
            if (pm.EprDate.HasValue)          return "ePR Raised";
            if (pm.QuoteDate.HasValue)        return "Quotation Requested";
            return "Not Started";
        }

        public void Dispose()
        {
            if (_ownsClipContext && _clip != null) _clip.Dispose();
        }
    }
}
