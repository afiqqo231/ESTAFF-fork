/* ============================================================
   ESTAFF — statutory report section on a task classification.

   The printed report is the Environment, Safety and Health Monthly Report
   filed under the Occupational Safety & Health (Safety & Health Officer)
   Regulations 1997. It has ten numbered parts fixed by the form, and every
   task has to be printed under one of them. This column is what says which.

   Values are ESTAFF.Models.Data.EshSection:

        1  Compliance Activity with S&H Related Regulation   19(2)(a)
        2  Methods of Establishing a Safe/Healthy Workplace  19(2)(b)
        3  Safety and Health Statistic                       19(2)(c)
        4  Machinery/Substance/Process Leading to Injuries    19(2)(d)
        5  Machinery/PPE Given for Minimizing Risk           19(2)(e)
        6  Purchase Request (PR) of the Month
        7  Layout Changes in The Premises                    19(2)(f)
        8  S&H Training, Promotions, Activities, Inspection  19(2)(g)
        9  Matters Arising, Unclosed Items                   19(2)(h)
       10  Feedback, Communication Received                  19(2)(i)

   Only 1, 2, 4, 5, 7, 8, 9 and 10 read their rows from tasks. Sections 3
   and 6 count incidents and list purchase orders, neither of which ESTAFF
   records, so they print as the blank statutory grid and are never a valid
   value here.

   NULL is allowed and means "not mapped": those tasks print under section 2,
   the part of the form that asks what was done to keep the workplace safe.

   Automatic migrations are deliberately off (Migrations/Configuration.cs):
   this database also hosts EHS_PORTAL's CLIP, CORD and FETS schemas, and
   ESTAFF's entities map only the columns it reads, so letting EF reconcile
   them would drop live columns owned by another application. Apply schema
   changes with this script instead.

   Safe to re-run: every statement is guarded.
   Touches only the ESTAFF schema.
============================================================ */

/* ── ESTAFF.TaskClassifications.ReportSection ─────────────────
   Which part of the printed statutory report this work stream feeds. */
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[ESTAFF].[TaskClassifications]')
      AND name = N'ReportSection')
BEGIN
    ALTER TABLE [ESTAFF].[TaskClassifications]
        ADD [ReportSection] INT NULL;

    PRINT 'Added ESTAFF.TaskClassifications.ReportSection';
END
ELSE
    PRINT 'ESTAFF.TaskClassifications.ReportSection already exists';
GO

/* ── Reject values that have nowhere to print ─────────────────
   Sections 3 and 6 are not task-backed, and there is no section 0 or 11.
   The application already refuses them; the column says so too, because
   this database is written to by more than one thing. */
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[ESTAFF].[TaskClassifications]')
      AND name = N'CK_TaskClassifications_ReportSection')
BEGIN
    ALTER TABLE [ESTAFF].[TaskClassifications]
        ADD CONSTRAINT [CK_TaskClassifications_ReportSection]
        CHECK ([ReportSection] IS NULL
               OR [ReportSection] IN (1, 2, 4, 5, 7, 8, 9, 10));

    PRINT 'Added CK_TaskClassifications_ReportSection';
END
ELSE
    PRINT 'CK_TaskClassifications_ReportSection already exists';
GO

/* ── Opening mapping for the classifications that ship today ──
   Only rows that have never been mapped are touched, so re-running this
   after an admin has changed a mapping on the Classifications screen will
   not undo their work.

   Chemical & Legal and DOSH / BOMBA / DOE are regulator-facing work, so
   they answer section 1. Environmental and CLIP are the recurring checks
   and monitoring that keep the workplace safe, which is section 2. Any
   classification added since is left NULL for an admin to map. */
UPDATE [ESTAFF].[TaskClassifications]
   SET [ReportSection] = 1
 WHERE [ReportSection] IS NULL
   AND ([Name] LIKE '%Chemical%'
     OR [Name] LIKE '%Legal%'
     OR [Name] LIKE '%DOSH%'
     OR [Name] LIKE '%BOMBA%'
     OR [Name] LIKE '%DOE%');

PRINT 'Mapped compliance classifications to section 1 ('
      + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' row(s))';
GO

UPDATE [ESTAFF].[TaskClassifications]
   SET [ReportSection] = 2
 WHERE [ReportSection] IS NULL
   AND ([Name] LIKE '%Environment%'
     OR [Name] = 'CLIP');

PRINT 'Mapped workplace classifications to section 2 ('
      + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' row(s))';
GO

/* ── What is left for an admin to do ──────────────────────────
   Anything still NULL prints under section 2 until it is mapped on
   /Classifications/Edit. Listed here so applying this script says so. */
SELECT [TaskClassificationId],
       [Name],
       'Not mapped - will print under section 2' AS [ReportSection]
  FROM [ESTAFF].[TaskClassifications]
 WHERE [ReportSection] IS NULL
 ORDER BY [TaskClassificationId];
GO
