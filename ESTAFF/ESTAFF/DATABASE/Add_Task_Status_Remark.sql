/* ============================================================
   ESTAFF — remarks on task status changes.

   Adds the one column this feature needs. The classification tables
   (ESTAFF.TaskClassifications, ESTAFF.TaskLists) and the TaskItems columns
   TaskClassificationId / TaskList_TaskListId / SubTaskId already exist and
   are left untouched.

   Automatic migrations are deliberately off (Migrations/Configuration.cs,
   Global.asax.cs): this database also hosts EHS_PORTAL's CLIP, CORD and
   FETS schemas, and ESTAFF's entities map only the columns it reads, so
   letting EF reconcile them would drop live columns owned by another
   application. Apply schema changes with this script instead.

   Safe to re-run: every statement is guarded.
   Touches only the ESTAFF schema.
============================================================ */

/* ── ESTAFF.TaskHistories.Remark ──────────────────────────────
   Free-text note captured with a status change and surfaced on the task
   as its "latest status remark". */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskHistories]')
      AND name = N'Remark')
BEGIN
    ALTER TABLE [ESTAFF].[TaskHistories]
        ADD [Remark] NVARCHAR(500) NULL;

    PRINT 'Added ESTAFF.TaskHistories.Remark';
END
ELSE
    PRINT 'ESTAFF.TaskHistories.Remark already exists';
GO

/* ── Index for the "latest status remark" lookup ──────────────
   Every task list page reads the newest StatusChanged row per task. */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskHistories]')
      AND name = N'IX_TaskHistories_TaskId_Action_ChangedDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TaskHistories_TaskId_Action_ChangedDate]
        ON [ESTAFF].[TaskHistories] ([TaskId], [Action], [ChangedDate] DESC);

    PRINT 'Created IX_TaskHistories_TaskId_Action_ChangedDate';
END
ELSE
    PRINT 'IX_TaskHistories_TaskId_Action_ChangedDate already exists';
GO

/* ── Backfill rows written before the remark was wired up ─────
   EmployeeController.UpdateStatus used to fold the employee's "action
   taken" text into NewValue instead of passing it as the remark, so those
   rows read:

       OldValue  'In Progress'
       NewValue  'Status: In Progress -> Complete. Action taken: <text>'
       Remark    NULL

   which left HasRemark false on the task and made the transition
   unparseable (ParseStatus expects the raw enum name). This recovers the
   remark and normalises the two value columns to match what
   TaskService.LogStatusChange writes now.

   Idempotent: each statement only matches rows still in the old shape. */
UPDATE [ESTAFF].[TaskHistories]
SET [Remark] = NULLIF(LTRIM(RTRIM(
        SUBSTRING([NewValue],
                  CHARINDEX('Action taken:', [NewValue]) + 13,
                  500))), '')
WHERE [Action] = 'StatusChanged'
  AND [Remark] IS NULL
  AND [NewValue] LIKE '%Action taken:%';
GO

PRINT 'Backfilled ESTAFF.TaskHistories.Remark from legacy NewValue text';
GO

/* NewValue -> raw enum name. Runs after the remark extraction above, which
   depends on the old text still being present. */
UPDATE [ESTAFF].[TaskHistories]
SET [NewValue] =
        CASE
            WHEN [NewValue] LIKE '%-> In Progress%' THEN 'InProgress'
            WHEN [NewValue] LIKE '%-> Complete%'    THEN 'Complete'
            WHEN [NewValue] LIKE '%-> Pending%'     THEN 'Pending'
            WHEN [NewValue] LIKE '%-> Overdue%'     THEN 'Overdue'
            ELSE [NewValue]
        END
WHERE [Action] = 'StatusChanged'
  AND [NewValue] LIKE 'Status:%->%';
GO

/* OldValue used the display label; only "In Progress" differs from the
   enum name. */
UPDATE [ESTAFF].[TaskHistories]
SET [OldValue] = 'InProgress'
WHERE [Action] = 'StatusChanged'
  AND [OldValue] = 'In Progress';
GO

PRINT 'Normalised ESTAFF.TaskHistories status transition values';
GO

/* ── Optional cleanup ─────────────────────────────────────────
   CLIP.Monitorings and CLIP.PlantMonitorings (plural) were created in the
   CLIP schema by ESTAFF's automatic migrations, which used the wrong table
   names. EHS_PORTAL's real data lives in the singular CLIP.Monitoring and
   CLIP.PlantMonitoring, which ESTAFF now reads instead.

   The stray tables are empty and unused. Review before running — they sit
   in a schema this application does not own, so the drop is left commented
   out deliberately.

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s
             ON s.schema_id = t.schema_id
           WHERE s.name = 'CLIP' AND t.name = 'PlantMonitorings')
   AND NOT EXISTS (SELECT 1 FROM [CLIP].[PlantMonitorings])
    DROP TABLE [CLIP].[PlantMonitorings];

IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s
             ON s.schema_id = t.schema_id
           WHERE s.name = 'CLIP' AND t.name = 'Monitorings')
   AND NOT EXISTS (SELECT 1 FROM [CLIP].[Monitorings])
    DROP TABLE [CLIP].[Monitorings];
*/
