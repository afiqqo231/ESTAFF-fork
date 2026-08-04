using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    // Produces the printed task report: a document that is signed off, filed
    // and read away from the system, so every task carries its full record
    // rather than a summary line that only makes sense next to the screen.
    //
    // Layout is deliberately conservative - one column, repeating table
    // headers, a running header and numbered pages - because it is printed,
    // scanned and attached to audits.
    public class ReportPdfService
    {
        // ── Palette ────────────────────────────────────────────
        private static readonly BaseColor Ink
            = new BaseColor(10, 22, 40);
        private static readonly BaseColor InkSoft
            = new BaseColor(51, 65, 85);
        private static readonly BaseColor Muted
            = new BaseColor(100, 116, 139);
        private static readonly BaseColor Accent
            = new BaseColor(16, 185, 129);
        private static readonly BaseColor Rule
            = new BaseColor(226, 232, 240);
        private static readonly BaseColor Panel
            = new BaseColor(248, 250, 252);
        private static readonly BaseColor White
            = BaseColor.WHITE;
        private static readonly BaseColor Danger
            = new BaseColor(239, 68, 68);
        private static readonly BaseColor Warning
            = new BaseColor(245, 158, 11);
        private static readonly BaseColor Info
            = new BaseColor(59, 130, 246);

        private const float ContentWidth = 523f;   // A4 less 36pt margins

        public byte[] GeneratePdf(ReportDetailViewModel vm)
        {
            var tasks = ResolveTasks(vm);

            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 36f, 36f, 40f, 46f);
                var writer = PdfWriter.GetInstance(doc, ms);

                writer.PageEvent = new PageFurniture(vm);

                doc.AddTitle(vm.ReportTypeLabel + " Task Report - "
                             + Text(vm.EmpName));
                doc.AddAuthor("ESTAFF");
                doc.AddSubject("Task report for " + vm.PeriodText);

                doc.Open();

                AddTitleBlock(doc, vm);
                AddReportMeta(doc, vm);
                AddExecutiveSummary(doc, vm, tasks);
                AddClassificationBreakdown(doc, tasks);
                AddTaskSchedule(doc, tasks);
                AddTaskDetails(doc, tasks);
                AddApprovalTrail(doc, vm);
                AddSignOff(doc, vm);

                doc.Close();
                return ms.ToArray();
            }
        }

        // The controllers fill TaskDetails. A caller that only set Tasks still
        // gets a valid document rather than an empty one.
        private static List<ReportTaskDetailViewModel> ResolveTasks(
            ReportDetailViewModel vm)
        {
            if (vm.TaskDetails != null && vm.TaskDetails.Any())
                return vm.TaskDetails;

            if (vm.Tasks == null) return new List<ReportTaskDetailViewModel>();

            return vm.Tasks
                .Select(t => ReportTaskDetailViewModel.From(t, null, null))
                .ToList();
        }

        // ══════════════════════════════════════════════════════
        // SECTIONS
        // ══════════════════════════════════════════════════════

        private static void AddTitleBlock(Document doc,
            ReportDetailViewModel vm)
        {
            var band = NewTable(new[] { 2.6f, 1.4f });
            band.SpacingAfter = 14f;

            var left = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = Ink,
                Padding         = 20f,
                PaddingBottom   = 22f
            };
            left.AddElement(Para("ESTAFF", Font(15f, Accent, true)));
            left.AddElement(Para(
                vm.ReportTypeLabel.ToUpper() + " TASK REPORT",
                Font(19f, White, true), 6f));
            left.AddElement(Para(
                Text(vm.EmpName) + "  |  " + vm.PeriodText,
                Font(9.5f, new BaseColor(203, 213, 225)), 6f));
            band.AddCell(left);

            var right = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = Ink,
                Padding         = 20f,
                PaddingBottom   = 22f,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            right.AddElement(Para("REFERENCE",
                Font(7f, new BaseColor(148, 163, 184)),
                0f, Element.ALIGN_RIGHT));
            right.AddElement(Para(vm.Reference,
                Font(11f, White, true), 2f, Element.ALIGN_RIGHT));
            right.AddElement(Para("STATUS",
                Font(7f, new BaseColor(148, 163, 184)),
                10f, Element.ALIGN_RIGHT));
            right.AddElement(Para(vm.Status.ToString().ToUpper(),
                Font(11f, ReportStatusColor(vm.Status), true),
                2f, Element.ALIGN_RIGHT));
            band.AddCell(right);

            doc.Add(band);
        }

        private static void AddReportMeta(Document doc,
            ReportDetailViewModel vm)
        {
            var table = NewTable(new[] { 1f, 1f, 1f });
            table.SpacingAfter = 18f;

            AddMetaCell(table, "Employee", Text(vm.EmpName));
            AddMetaCell(table, "Employee No.", Text(vm.EmpNumber));
            AddMetaCell(table, "Email", Text(vm.EmpEmail));

            AddMetaCell(table, "Reporting Period", vm.PeriodText);
            AddMetaCell(table, "Period Length",
                vm.PeriodDays + (vm.PeriodDays == 1 ? " day" : " days"));
            AddMetaCell(table, "Report Type",
                vm.ReportTypeLabel + " report");

            doc.Add(table);
        }

        private static void AddExecutiveSummary(Document doc,
            ReportDetailViewModel vm, List<ReportTaskDetailViewModel> tasks)
        {
            doc.Add(Heading("Executive Summary"));

            var total       = tasks.Count;
            var complete    = tasks.Count(t => t.Status == TaskStatus.Complete);
            var inProgress  = tasks.Count(t => t.Status == TaskStatus.InProgress);
            var pending     = tasks.Count(t => t.Status == TaskStatus.Pending);
            var overdue     = tasks.Count(t => t.Status == TaskStatus.Overdue);
            var rate        = total > 0
                ? Math.Round((decimal)complete / total * 100, 1)
                : 0m;

            var stats = NewTable(new[] { 1f, 1f, 1f, 1f, 1f });
            stats.SpacingAfter = 10f;

            AddStatCell(stats, "Total Tasks", total, Ink, Panel);
            AddStatCell(stats, "Completed", complete, Accent,
                new BaseColor(236, 253, 245));
            AddStatCell(stats, "In Progress", inProgress, Info,
                new BaseColor(239, 246, 255));
            AddStatCell(stats, "Not Started", pending, Warning,
                new BaseColor(255, 251, 235));
            AddStatCell(stats, "Overdue", overdue, Danger,
                new BaseColor(254, 242, 242));

            doc.Add(stats);
            doc.Add(CompletionBar(rate));

            // The narrative a manager reads before anything else.
            var narrative = total == 0
                ? "No tasks fell within this reporting period."
                : string.Format(
                    "{0} of {1} {2} completed ({3}%). {4} in progress, " +
                    "{5} not started, {6} overdue.",
                    complete, total, total == 1 ? "task was" : "tasks were",
                    rate.ToString("0.#"), inProgress, pending, overdue);

            var summary = new PdfPTable(1) { WidthPercentage = 100 };
            var cell = new PdfPCell
            {
                Border          = Rectangle.LEFT_BORDER,
                BorderWidthLeft = 3f,
                BorderColorLeft = overdue > 0 ? Danger : Accent,
                BackgroundColor = Panel,
                Padding         = 12f
            };
            cell.AddElement(Para(narrative, Font(9f, InkSoft)));

            if (overdue > 0)
            {
                cell.AddElement(Para(
                    "Attention: " + overdue + (overdue == 1
                        ? " task passed its due date without completion."
                        : " tasks passed their due date without completion."),
                    Font(9f, Danger, true), 5f));
            }

            summary.AddCell(cell);
            summary.SpacingAfter = 18f;
            doc.Add(summary);
        }

        private static void AddClassificationBreakdown(Document doc,
            List<ReportTaskDetailViewModel> tasks)
        {
            if (!tasks.Any()) return;

            doc.Add(Heading("Breakdown by Classification"));

            var table = NewTable(new[] { 2.6f, 0.8f, 0.9f, 0.9f, 0.9f, 1f });
            table.SpacingAfter = 18f;
            table.HeaderRows = 1;

            AddTh(table, "Classification");
            AddTh(table, "Tasks", Element.ALIGN_CENTER);
            AddTh(table, "Complete", Element.ALIGN_CENTER);
            AddTh(table, "Ongoing", Element.ALIGN_CENTER);
            AddTh(table, "Overdue", Element.ALIGN_CENTER);
            AddTh(table, "Rate", Element.ALIGN_RIGHT);

            var groups = tasks
                .GroupBy(t => t.ClassificationLabel)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key);

            var alt = false;
            foreach (var group in groups)
            {
                var bg = alt ? Panel : White;
                alt = !alt;

                var count    = group.Count();
                var complete = group.Count(t => t.Status == TaskStatus.Complete);
                var ongoing  = group.Count(t =>
                    t.Status == TaskStatus.Pending
                    || t.Status == TaskStatus.InProgress);
                var overdue  = group.Count(t => t.Status == TaskStatus.Overdue);
                var rate     = Math.Round(
                    (decimal)complete / count * 100, 0);

                AddTd(table, Text(group.Key), bg, Ink, true);
                AddTd(table, count.ToString(), bg,
                    align: Element.ALIGN_CENTER);
                AddTd(table, complete.ToString(), bg,
                    complete > 0 ? Accent : Muted,
                    align: Element.ALIGN_CENTER);
                AddTd(table, ongoing.ToString(), bg,
                    align: Element.ALIGN_CENTER);
                AddTd(table, overdue.ToString(), bg,
                    overdue > 0 ? Danger : Muted, overdue > 0,
                    Element.ALIGN_CENTER);
                AddTd(table, rate.ToString("0") + "%", bg, Ink, true,
                    Element.ALIGN_RIGHT);
            }

            doc.Add(table);
        }

        private static void AddTaskSchedule(Document doc,
            List<ReportTaskDetailViewModel> tasks)
        {
            doc.Add(Heading("Task Schedule"));

            if (!tasks.Any())
            {
                doc.Add(EmptyNote(
                    "No tasks were recorded for this period."));
                return;
            }

            // Widths are set so no column header and no formatted date has to
            // wrap; only the task title, which is meant to.
            var table = NewTable(
                new[] { 0.45f, 2.9f, 1.75f, 1.15f, 1.3f, 1.3f, 1.15f });
            table.SpacingAfter = 18f;
            table.HeaderRows = 1;

            AddTh(table, "#", Element.ALIGN_CENTER);
            AddTh(table, "Task");
            AddTh(table, "Classification");
            AddTh(table, "Priority");
            AddTh(table, "Due");
            AddTh(table, "Completed");
            AddTh(table, "Status");

            var index = 0;
            var alt = false;
            foreach (var task in tasks)
            {
                index++;
                var bg = alt ? Panel : White;
                alt = !alt;

                AddTd(table, index.ToString("00"), bg, Muted,
                    align: Element.ALIGN_CENTER);
                AddTd(table, Text(task.Title), bg, Ink, true);
                AddTd(table, Text(task.ClassificationLabel), bg);
                AddTd(table, task.PriorityLabel, bg);
                AddTd(table, task.DueDate.ToString("dd MMM yyyy"), bg,
                    task.IsOverdue ? Danger : Muted, task.IsOverdue);
                AddTd(table, DateText(task.CompletedDate), bg);
                AddTd(table, task.StatusLabel, bg,
                    StatusColor(task.Status), true);
            }

            doc.Add(table);
        }

        private static void AddTaskDetails(Document doc,
            List<ReportTaskDetailViewModel> tasks)
        {
            if (!tasks.Any()) return;

            doc.Add(Heading("Task Details"));
            doc.Add(Para(
                "Every task in the period with the actions recorded against it.",
                Font(8.5f, Muted), 0f, Element.ALIGN_LEFT, 12f));

            var index = 0;
            foreach (var task in tasks)
            {
                index++;
                doc.Add(TaskBlock(task, index));
            }
        }

        // One task, printed as a self-contained block.
        private static PdfPTable TaskBlock(ReportTaskDetailViewModel task,
            int index)
        {
            var block = new PdfPTable(1)
            {
                WidthPercentage = 100,
                SpacingAfter    = 14f,
                KeepTogether    = true
            };

            var shell = new PdfPCell
            {
                Border          = Rectangle.BOX,
                BorderWidth     = 0.7f,
                BorderColor     = Rule,
                Padding         = 0f
            };

            // Title bar
            var head = NewTable(new[] { 3.4f, 1.1f });
            var titleCell = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = Ink,
                Padding         = 10f
            };
            titleCell.AddElement(Para(
                "TASK " + index.ToString("00") + "  |  ID " + task.TaskId,
                Font(7f, new BaseColor(148, 163, 184))));
            titleCell.AddElement(Para(Text(task.Title),
                Font(11f, White, true), 3f));
            head.AddCell(titleCell);

            var statusCell = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = Ink,
                Padding         = 10f,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            statusCell.AddElement(Para(task.StatusLabel.ToUpper(),
                Font(10f, StatusColor(task.Status), true),
                8f, Element.ALIGN_RIGHT));
            statusCell.AddElement(Para(Text(task.DeliveryText),
                Font(7.5f, new BaseColor(203, 213, 225)),
                3f, Element.ALIGN_RIGHT));
            head.AddCell(statusCell);

            shell.AddElement(head);

            // Field grid
            var grid = NewTable(new[] { 1.15f, 1.85f, 1.15f, 1.85f });
            AddField(grid, "Classification", Text(task.ClassificationLabel));
            AddField(grid, "Task Type",
                string.IsNullOrWhiteSpace(task.TaskListName)
                    ? "Not specified"
                    : Text(task.TaskListName));
            AddField(grid, "Priority", task.PriorityLabel);
            AddField(grid, "Status", task.StatusLabel);
            AddField(grid, "Assigned To", Text(task.AssignedToName));
            AddField(grid, "Assigned By", Text(task.AssignedByName));
            AddField(grid, "Assigned On",
                task.AssignedDate.ToString("dd MMM yyyy"));
            AddField(grid, "Due Date",
                task.DueDate.ToString("dd MMM yyyy"),
                task.IsOverdue ? Danger : Ink);
            AddField(grid, "Completed On", DateText(task.CompletedDate));
            AddField(grid, "Last Updated",
                task.LastModifiedDate.ToString("dd MMM yyyy, h:mm tt"));
            shell.AddElement(grid);

            // CLIP record, when the task covers one
            if (task.ClipItem != null)
            {
                var clip = task.ClipItem;
                var line = clip.KindLabel + " - " + Text(clip.Title);
                if (!string.IsNullOrWhiteSpace(clip.Subtitle))
                    line += " (" + Text(clip.Subtitle) + ")";

                var detail = "Plant: " + Text(clip.PlantName)
                    + "   |   Expiry: " + Text(clip.ExpiryDateText)
                    + "   |   " + Text(clip.ExpiryStatus);

                if (!string.IsNullOrWhiteSpace(clip.ProcessStatus))
                    detail += "   |   Progress: " + Text(clip.ProcessStatus);

                shell.AddElement(SubSection("CLIP Record", line, detail,
                    clip.Urgency == ClipUrgency.Expired ? Danger
                        : clip.Urgency == ClipUrgency.ExpiringSoon ? Warning
                        : Accent));
            }

            // Description
            shell.AddElement(SubSection("Description",
                string.IsNullOrWhiteSpace(task.Description)
                    ? "No description was provided."
                    : Text(task.Description),
                null,
                Rule,
                string.IsNullOrWhiteSpace(task.Description)));

            // Actions
            shell.AddElement(ActionsSection(task));

            block.AddCell(shell);
            return block;
        }

        // The action history: what was done, in the order it happened.
        private static IElement ActionsSection(
            ReportTaskDetailViewModel task)
        {
            var wrap = new PdfPTable(1) { WidthPercentage = 100 };
            var cell = new PdfPCell
            {
                Border  = Rectangle.TOP_BORDER,
                BorderWidthTop = 0.7f,
                BorderColorTop = Rule,
                Padding = 10f
            };

            cell.AddElement(Para("ACTIONS TAKEN", Font(7f, Muted, true)));

            if (task.Actions == null || !task.Actions.Any())
            {
                cell.AddElement(Para(
                    "No status changes were recorded for this task.",
                    Font(8.5f, Muted), 5f));
                wrap.AddCell(cell);
                return wrap;
            }

            var table = NewTable(new[] { 1.3f, 1.8f, 3.65f, 1.05f });
            table.SpacingBefore = 6f;
            table.HeaderRows = 1;

            AddTh(table, "When", size: 7f);
            AddTh(table, "Status Change", size: 7f);
            AddTh(table, "Action Taken", size: 7f);
            AddTh(table, "By", size: 7f);

            var alt = false;
            foreach (var action in task.Actions)
            {
                var bg = alt ? Panel : White;
                alt = !alt;

                AddTd(table, action.ChangedDate.ToString("dd MMM yyyy\nh:mm tt"),
                    bg, Muted, size: 7.5f);
                AddTd(table, Text(action.TransitionText), bg,
                    StatusColor(action.ToStatus), true, size: 7.5f);

                // The remark is the point of the row: printed in full, never
                // clipped, and called out when it is missing.
                AddTd(table,
                    action.HasRemark
                        ? Text(action.Remark)
                        : StatusRemarkViewModel.NoActionText,
                    bg,
                    action.HasRemark ? InkSoft : Muted,
                    size: 8f,
                    italic: !action.HasRemark);

                AddTd(table, Text(action.ChangedByName), bg, Muted,
                    size: 7.5f);
            }

            cell.AddElement(table);
            wrap.AddCell(cell);
            return wrap;
        }

        private static void AddApprovalTrail(Document doc,
            ReportDetailViewModel vm)
        {
            doc.Add(Heading("Approval Trail"));

            var table = NewTable(new[] { 1f, 1f, 1f, 1f });
            table.SpacingAfter = vm.Status == ReportStatus.Rejected ? 10f : 20f;

            AddMetaCell(table, "Created", DateTimeText(vm.CreatedDate));
            AddMetaCell(table, "Submitted", DateTimeText(vm.SubmittedDate));
            AddMetaCell(table, vm.Status == ReportStatus.Rejected
                ? "Reviewed" : "Approved", DateTimeText(vm.ApprovedDate));
            AddMetaCell(table, "Current Status", vm.Status.ToString());

            doc.Add(table);

            if (vm.Status == ReportStatus.Rejected
                && !string.IsNullOrWhiteSpace(vm.RejectionReason))
            {
                var reject = new PdfPTable(1) { WidthPercentage = 100 };
                var cell = new PdfPCell
                {
                    Border          = Rectangle.LEFT_BORDER,
                    BorderWidthLeft = 3f,
                    BorderColorLeft = Danger,
                    BackgroundColor = new BaseColor(254, 242, 242),
                    Padding         = 12f
                };
                cell.AddElement(Para("REASON FOR REJECTION",
                    Font(7f, Danger, true)));
                cell.AddElement(Para(Text(vm.RejectionReason),
                    Font(9f, Ink), 4f));
                reject.AddCell(cell);
                reject.SpacingAfter = 20f;
                doc.Add(reject);
            }
        }

        private static void AddSignOff(Document doc,
            ReportDetailViewModel vm)
        {
            var table = NewTable(new[] { 1f, 0.2f, 1f });
            table.SpacingBefore = 6f;

            table.AddCell(SignatureCell("Prepared by",
                Text(vm.EmpName), Text(vm.EmpNumber)));
            table.AddCell(new PdfPCell { Border = Rectangle.NO_BORDER });
            table.AddCell(SignatureCell("Reviewed and approved by",
                "", "Name, designation and date"));

            doc.Add(table);
        }

        private static PdfPCell SignatureCell(string role, string name,
            string caption)
        {
            var cell = new PdfPCell
            {
                Border  = Rectangle.NO_BORDER,
                Padding = 0f
            };

            cell.AddElement(Para(role.ToUpper(), Font(7f, Muted, true)));

            // The rule people actually sign on.
            var line = new PdfPTable(1) { WidthPercentage = 100 };
            var lineCell = new PdfPCell(new Phrase(" ", Font(8f, Ink)))
            {
                Border          = Rectangle.BOTTOM_BORDER,
                BorderWidthBottom = 0.7f,
                BorderColorBottom = Muted,
                FixedHeight     = 34f
            };
            line.AddCell(lineCell);
            line.SpacingBefore = 4f;
            cell.AddElement(line);

            if (!string.IsNullOrWhiteSpace(name))
                cell.AddElement(Para(name, Font(9f, Ink, true), 4f));

            cell.AddElement(Para(caption, Font(7.5f, Muted),
                string.IsNullOrWhiteSpace(name) ? 4f : 1f));

            return cell;
        }

        // ══════════════════════════════════════════════════════
        // BUILDING BLOCKS
        // ══════════════════════════════════════════════════════

        private static PdfPTable NewTable(float[] widths)
        {
            var table = new PdfPTable(widths.Length)
            {
                WidthPercentage = 100
            };
            table.SetWidths(widths);
            return table;
        }

        private static Paragraph Heading(string text)
        {
            return new Paragraph(text, Font(12f, Ink, true))
            {
                SpacingBefore = 4f,
                SpacingAfter  = 8f
            };
        }

        private static Paragraph Para(string text, Font font,
            float spacingBefore = 0f,
            int alignment = Element.ALIGN_LEFT,
            float spacingAfter = 0f)
        {
            return new Paragraph(text ?? "", font)
            {
                SpacingBefore = spacingBefore,
                SpacingAfter  = spacingAfter,
                Alignment     = alignment,
                Leading       = font.Size * 1.35f
            };
        }

        private static void AddMetaCell(PdfPTable table, string label,
            string value)
        {
            var cell = new PdfPCell
            {
                Border          = Rectangle.BOTTOM_BORDER,
                BorderWidthBottom = 0.7f,
                BorderColorBottom = Rule,
                Padding         = 9f,
                PaddingTop      = 7f
            };
            cell.AddElement(Para(label.ToUpper(), Font(6.8f, Muted, true)));
            cell.AddElement(Para(
                string.IsNullOrWhiteSpace(value) ? "-" : value,
                Font(9.5f, Ink, true), 3f));
            table.AddCell(cell);
        }

        private static void AddStatCell(PdfPTable table, string label,
            int value, BaseColor color, BaseColor background)
        {
            var cell = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = background,
                Padding         = 12f
            };
            cell.AddElement(Para(value.ToString(), Font(17f, color, true),
                0f, Element.ALIGN_CENTER));
            cell.AddElement(Para(label.ToUpper(), Font(6.8f, Muted, true),
                2f, Element.ALIGN_CENTER));
            table.AddCell(cell);
        }

        // A drawn bar rather than a printed percentage: the point of the
        // number is the comparison, and a bar carries that at a glance.
        private static PdfPTable CompletionBar(decimal rate)
        {
            var wrap = new PdfPTable(1) { WidthPercentage = 100 };
            wrap.SpacingAfter = 12f;

            var cell = new PdfPCell
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = Panel,
                Padding         = 12f
            };

            var caption = NewTable(new[] { 1f, 1f });
            var label = new PdfPCell(new Phrase("COMPLETION RATE",
                Font(7f, Muted, true)))
            {
                Border = Rectangle.NO_BORDER
            };
            var value = new PdfPCell(new Phrase(rate.ToString("0.#") + "%",
                Font(11f, rate >= 50 ? Accent : Warning, true)))
            {
                Border = Rectangle.NO_BORDER,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            caption.AddCell(label);
            caption.AddCell(value);
            cell.AddElement(caption);

            // Two cells sized to the ratio. Both stay above zero width because
            // a zero-width column is not a valid table.
            var filled = (float)Math.Max(0.4m, Math.Min(99.6m, rate));
            var bar = NewTable(new[] { filled, 100f - filled });
            bar.SpacingBefore = 5f;
            bar.AddCell(new PdfPCell(new Phrase(" ", Font(4f, White)))
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = rate >= 50 ? Accent : Warning,
                FixedHeight     = 7f
            });
            bar.AddCell(new PdfPCell(new Phrase(" ", Font(4f, White)))
            {
                Border          = Rectangle.NO_BORDER,
                BackgroundColor = new BaseColor(226, 232, 240),
                FixedHeight     = 7f
            });
            cell.AddElement(bar);

            wrap.AddCell(cell);
            return wrap;
        }

        private static void AddField(PdfPTable table, string label,
            string value, BaseColor color = null)
        {
            var labelCell = new PdfPCell(new Phrase(label.ToUpper(),
                Font(6.8f, Muted, true)))
            {
                Border      = Rectangle.NO_BORDER,
                PaddingLeft = 10f,
                PaddingTop  = 7f,
                PaddingBottom = 2f
            };
            var valueCell = new PdfPCell(new Phrase(
                string.IsNullOrWhiteSpace(value) ? "-" : value,
                Font(8.5f, color ?? Ink)))
            {
                Border        = Rectangle.NO_BORDER,
                PaddingTop    = 7f,
                PaddingBottom = 2f,
                PaddingRight  = 10f
            };
            table.AddCell(labelCell);
            table.AddCell(valueCell);
        }

        private static IElement SubSection(string title, string body,
            string detail, BaseColor accent, bool bodyMuted = false)
        {
            var wrap = new PdfPTable(1) { WidthPercentage = 100 };
            var cell = new PdfPCell
            {
                Border         = Rectangle.TOP_BORDER,
                BorderWidthTop = 0.7f,
                BorderColorTop = Rule,
                Padding        = 10f
            };

            cell.AddElement(Para(title.ToUpper(), Font(7f, Muted, true)));
            cell.AddElement(Para(body,
                bodyMuted
                    ? FontItalic(8.5f, Muted)
                    : Font(8.5f, InkSoft), 4f));

            if (!string.IsNullOrWhiteSpace(detail))
                cell.AddElement(Para(detail, Font(8f, accent, true), 3f));

            wrap.AddCell(cell);
            return wrap;
        }

        private static PdfPTable EmptyNote(string text)
        {
            var table = new PdfPTable(1) { WidthPercentage = 100 };
            table.SpacingAfter = 18f;
            var cell = new PdfPCell(new Phrase(text, FontItalic(9f, Muted)))
            {
                Border          = Rectangle.BOX,
                BorderWidth     = 0.7f,
                BorderColor     = Rule,
                BackgroundColor = Panel,
                Padding         = 14f,
                HorizontalAlignment = Element.ALIGN_CENTER
            };
            table.AddCell(cell);
            return table;
        }

        private static void AddTh(PdfPTable table, string text,
            int align = Element.ALIGN_LEFT, float size = 7.5f)
        {
            table.AddCell(new PdfPCell(new Phrase(text.ToUpper(),
                Font(size, White, true)))
            {
                BackgroundColor = Ink,
                Border          = Rectangle.NO_BORDER,
                Padding         = 7f,
                HorizontalAlignment = align
            });
        }

        private static void AddTd(PdfPTable table, string text,
            BaseColor background, BaseColor color = null, bool bold = false,
            int align = Element.ALIGN_LEFT, float size = 8f,
            bool italic = false)
        {
            var font = italic
                ? FontItalic(size, color ?? Muted)
                : Font(size, color ?? Muted, bold);

            table.AddCell(new PdfPCell(new Phrase(text ?? "", font))
            {
                BackgroundColor = background,
                Border            = Rectangle.BOTTOM_BORDER,
                BorderWidthBottom = 0.5f,
                BorderColorBottom = Rule,
                Padding           = 6.5f,
                HorizontalAlignment = align
            });
        }

        private static Font Font(float size, BaseColor color,
            bool bold = false)
        {
            return FontFactory.GetFont(
                bold ? FontFactory.HELVETICA_BOLD : FontFactory.HELVETICA,
                size, color);
        }

        private static Font FontItalic(float size, BaseColor color)
        {
            return FontFactory.GetFont(
                FontFactory.HELVETICA_OBLIQUE, size, color);
        }

        // ══════════════════════════════════════════════════════
        // VALUES
        // ══════════════════════════════════════════════════════

        // The built-in PDF fonts encode Cp1252, which has no arrow. Remarks are
        // free text typed by employees, so the few characters the UI uses that
        // fall outside it are folded down to their plain equivalents rather
        // than dropped silently by the renderer.
        private static string Text(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            return value
                .Replace("→", "->")   // arrow, from TransitionText
                .Replace("←", "<-")
                .Replace("•", "-")    // bullet
                .Replace("…", "...")
                .Replace(" ", " ")    // non-breaking space
                .Replace("\r\n", "\n")
                .Trim();
        }

        private static string DateText(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd MMM yyyy")
                : "-";
        }

        private static string DateTimeText(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("dd MMM yyyy, h:mm tt")
                : "-";
        }

        private static BaseColor StatusColor(TaskStatus? status)
        {
            if (!status.HasValue) return Muted;

            switch (status.Value)
            {
                case TaskStatus.Complete:   return Accent;
                case TaskStatus.Overdue:    return Danger;
                case TaskStatus.InProgress: return Info;
                default:                    return Warning;
            }
        }

        private static BaseColor ReportStatusColor(ReportStatus status)
        {
            switch (status)
            {
                case ReportStatus.Approved:  return Accent;
                case ReportStatus.Rejected:  return Danger;
                case ReportStatus.Submitted: return Info;
                default:                     return new BaseColor(203, 213, 225);
            }
        }

        // ══════════════════════════════════════════════════════
        // PAGE FURNITURE
        // ══════════════════════════════════════════════════════

        // Running header and numbered footer. A report that is printed and
        // filed has to say what it is on every sheet, and a reader has to be
        // able to tell whether a page is missing.
        private class PageFurniture : PdfPageEventHelper
        {
            private readonly ReportDetailViewModel _vm;
            private readonly string _generated;

            private PdfTemplate _pageCount;
            private BaseFont _font;
            private int _pages;

            public PageFurniture(ReportDetailViewModel vm)
            {
                _vm = vm;
                _generated = DateTime.Now.ToString("dd MMM yyyy, h:mm tt");
            }

            public override void OnOpenDocument(PdfWriter writer,
                Document document)
            {
                _pageCount = writer.DirectContent.CreateTemplate(30f, 10f);
                _font = BaseFont.CreateFont(BaseFont.HELVETICA,
                    BaseFont.WINANSI, BaseFont.NOT_EMBEDDED);
            }

            public override void OnEndPage(PdfWriter writer,
                Document document)
            {
                _pages = writer.PageNumber;

                var canvas = writer.DirectContent;
                var left   = document.LeftMargin;
                var right  = document.PageSize.Width - document.RightMargin;

                // Running header, from the second page on - the first page
                // already carries the full title band.
                if (writer.PageNumber > 1)
                {
                    var top = document.PageSize.Height
                              - document.TopMargin + 18f;

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT,
                        new Phrase("ESTAFF  |  " + _vm.ReportTypeLabel
                            + " Task Report",
                            Font(7.5f, Ink, true)),
                        left, top, 0);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_RIGHT,
                        new Phrase(_vm.Reference + "  |  " + _vm.PeriodText,
                            Font(7.5f, Muted)),
                        right, top, 0);

                    canvas.SetColorStroke(Rule);
                    canvas.SetLineWidth(0.7f);
                    canvas.MoveTo(left, top - 5f);
                    canvas.LineTo(right, top - 5f);
                    canvas.Stroke();
                }

                // Footer
                var footerY = document.BottomMargin - 16f;

                canvas.SetColorStroke(Rule);
                canvas.SetLineWidth(0.7f);
                canvas.MoveTo(left, footerY + 14f);
                canvas.LineTo(right, footerY + 14f);
                canvas.Stroke();

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT,
                    new Phrase("ESTAFF  |  Confidential - internal use only",
                        Font(7f, Muted)),
                    left, footerY, 0);

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_RIGHT,
                    new Phrase("Generated " + _generated,
                        Font(7f, Muted)),
                    right, footerY, 0);

                // "Page 2 of " here, with the total stamped in once the
                // document knows how many pages it ran to.
                var centre = (left + right) / 2f;
                var label = new Phrase("Page " + writer.PageNumber + " of ",
                    Font(7f, Muted));
                var labelWidth = _font.GetWidthPoint(
                    "Page " + writer.PageNumber + " of ", 7f);

                ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT, label,
                    centre - (labelWidth / 2f), footerY, 0);

                canvas.AddTemplate(_pageCount,
                    centre + (labelWidth / 2f), footerY);
            }

            public override void OnCloseDocument(PdfWriter writer,
                Document document)
            {
                // Counted from the pages actually written rather than the
                // writer's next-page number, which is one ahead here.
                _pageCount.BeginText();
                _pageCount.SetFontAndSize(_font, 7f);
                _pageCount.SetColorFill(Muted);
                _pageCount.SetTextMatrix(0, 0);
                _pageCount.ShowText(_pages.ToString());
                _pageCount.EndText();
            }
        }
    }
}
