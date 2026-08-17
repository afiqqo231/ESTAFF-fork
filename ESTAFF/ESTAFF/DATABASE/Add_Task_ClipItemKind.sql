/* ============================================================
   ESTAFF — attach a CLIP record to any task.

   A task could previously only reach a CLIP record by being filed under a
   classification named "CLIP", and which CLIP table its SubTaskId pointed
   at was inferred from the name of its TaskList:

        "Certificate Of FItness"  ->  CLIP.CertificateOfFitness
        "Plant Monitoring"        ->  CLIP.PlantMonitoring

   That made covering a certificate and describing the kind of work the same
   choice, so only one of the two could be answered — and the classification
   had to say where the work came from instead of what it was.

   ESTAFF.TaskItems.ClipItemKind records the table outright. The attachment
   is now (ClipItemKind, SubTaskId), independent of classification and task
   type: any task may carry one, and most carry none.

        1  Certificate of Fitness   (CLIP.CertificateOfFitness.Id)
        2  Plant Monitoring         (CLIP.PlantMonitoring.Id)

   NULL means no attached record. So does a NULL SubTaskId — the application
   treats half a link as none.

   Automatic migrations are deliberately off (Migrations/Configuration.cs):
   this database also hosts EHS_PORTAL's CLIP, CORD and FETS schemas, and
   ESTAFF's entities map only the columns it reads, so letting EF reconcile
   them would drop live columns owned by another application. Apply schema
   changes with this script instead.

   Safe to re-run: every statement is guarded, and the backfill only fills
   rows that are still NULL.
   Touches only the ESTAFF schema. Reads CLIP but never writes to it.
============================================================ */

/* ── ESTAFF.TaskItems.ClipItemKind ────────────────────────────
   Which CLIP table this task's SubTaskId points at. */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
      AND name = N'ClipItemKind')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems]
        ADD [ClipItemKind] INT NULL;

    PRINT 'Added ESTAFF.TaskItems.ClipItemKind';
END
ELSE
    PRINT 'ESTAFF.TaskItems.ClipItemKind already exists';
GO

/* ── Only the two tables exist ────────────────────────────────
   The application never writes anything else; the column says so too,
   because this database is written to by more than one thing. */
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[ESTAFF].[TaskItems]')
      AND name = N'CK_TaskItems_ClipItemKind')
BEGIN
    ALTER TABLE [ESTAFF].[TaskItems]
        ADD CONSTRAINT [CK_TaskItems_ClipItemKind]
        CHECK ([ClipItemKind] IS NULL OR [ClipItemKind] IN (1, 2));

    PRINT 'Added CK_TaskItems_ClipItemKind';
END
ELSE
    PRINT 'CK_TaskItems_ClipItemKind already exists';
GO

/* ── Backfill from the old rule ───────────────────────────────
   Every task that already points at a CLIP record keeps pointing at it.
   The kind is recovered from its task list name using exactly the match
   the code used to apply at read time (ClipService.ClassifyTaskListName):
   "fitness" or "cof" meant a certificate, "monitoring" meant monitoring —
   loose enough to survive the "Certificate Of FItness" typo in the seeded
   data.

   Only rows with a SubTaskId and no kind yet are touched, so re-running
   this will not overwrite anything set since. */
UPDATE  t
   SET  t.[ClipItemKind] = 1
  FROM  [ESTAFF].[TaskItems] t
  JOIN  [ESTAFF].[TaskLists] l
    ON  l.[TaskListId] = t.[TaskList_TaskListId]
 WHERE  t.[ClipItemKind] IS NULL
   AND  t.[SubTaskId] IS NOT NULL
   AND (LOWER(l.[Name]) LIKE '%fitness%' OR LOWER(l.[Name]) LIKE '%cof%');

PRINT 'Backfilled Certificate of Fitness links ('
      + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' row(s))';
GO

UPDATE  t
   SET  t.[ClipItemKind] = 2
  FROM  [ESTAFF].[TaskItems] t
  JOIN  [ESTAFF].[TaskLists] l
    ON  l.[TaskListId] = t.[TaskList_TaskListId]
 WHERE  t.[ClipItemKind] IS NULL
   AND  t.[SubTaskId] IS NOT NULL
   AND  LOWER(l.[Name]) LIKE '%monitoring%';

PRINT 'Backfilled Plant Monitoring links ('
      + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' row(s))';
GO

/* ── Anything the old rule could not name ─────────────────────
   A SubTaskId whose task list name matched neither pattern was already
   being ignored by the application before this change — GetItemsForTasks
   skipped it, so no CLIP record was ever shown for it. Clearing it now
   makes the data say what the behaviour already was, rather than leaving
   an id that points at nothing identifiable. */
UPDATE [ESTAFF].[TaskItems]
   SET [SubTaskId] = NULL
 WHERE [ClipItemKind] IS NULL
   AND [SubTaskId] IS NOT NULL;

PRINT 'Cleared unidentifiable CLIP links ('
      + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' row(s))';
GO

/* ── What is left for an admin to do ──────────────────────────
   The classification named "CLIP" is now an ordinary row. It is not
   dropped here: its tasks and task types are real records, and deciding
   where they belong is a judgement about this site's work, not something
   a migration should guess.

   Those tasks keep their attached CLIP record whatever happens to the
   classification. Reclassify them on /Admin/Tasks and then delete the row
   on /Classifications once nothing points at it. Until then it behaves
   like any other classification, and its ESH report section can be set
   like any other.

   This lists what is still filed there. */
SELECT  c.[TaskClassificationId],
        c.[Name],
        (SELECT COUNT(*) FROM [ESTAFF].[TaskItems] ti
          WHERE ti.[TaskClassificationId] = c.[TaskClassificationId])
            AS [Tasks],
        (SELECT COUNT(*) FROM [ESTAFF].[TaskLists] tl
          WHERE tl.[TaskClassificationId] = c.[TaskClassificationId])
            AS [TaskTypes]
  FROM  [ESTAFF].[TaskClassifications] c
 WHERE  c.[Name] = 'CLIP';
GO

/* Tasks now carrying an attached CLIP record, for a quick sanity check. */
SELECT  COUNT(*) AS [TasksWithClipRecord],
        SUM(CASE WHEN [ClipItemKind] = 1 THEN 1 ELSE 0 END)
            AS [CertificateOfFitness],
        SUM(CASE WHEN [ClipItemKind] = 2 THEN 1 ELSE 0 END)
            AS [PlantMonitoring]
  FROM  [ESTAFF].[TaskItems]
 WHERE  [ClipItemKind] IS NOT NULL
   AND  [SubTaskId] IS NOT NULL;
GO
