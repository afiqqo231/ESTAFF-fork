using System;
using System.Collections.Generic;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using ESTAFF.Models.Data;
using ESTAFF.Models.ViewModels;

namespace ESTAFF.Services
{
    public class ReportPdfService
    {
        // Colors
        private static readonly BaseColor ColorPrimary
            = new BaseColor(16, 185, 129);
        private static readonly BaseColor ColorDark
            = new BaseColor(10, 22, 40);
        private static readonly BaseColor ColorGray
            = new BaseColor(100, 116, 139);
        private static readonly BaseColor ColorLight
            = new BaseColor(248, 250, 249);
        private static readonly BaseColor ColorBorder
            = new BaseColor(209, 250, 229);
        private static readonly BaseColor ColorWhite
            = BaseColor.WHITE;
        private static readonly BaseColor ColorDanger
            = new BaseColor(239, 68, 68);
        private static readonly BaseColor ColorWarning
            = new BaseColor(245, 158, 11);

        public byte[] GeneratePdf(ReportDetailViewModel vm)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(
                    PageSize.A4, 40f, 40f, 40f, 40f);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // ── Fonts ──────────────────────────────────
                var fontTitle  = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 22f, ColorDark);
                var fontH2     = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 13f, ColorDark);
                var fontH3     = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 10f, ColorDark);
                var fontBody   = FontFactory.GetFont(
                    FontFactory.HELVETICA, 9f, ColorGray);
                var fontSmall  = FontFactory.GetFont(
                    FontFactory.HELVETICA, 8f, ColorGray);
                var fontBold   = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 9f, ColorDark);
                var fontWhite  = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 9f, ColorWhite);
                var fontGreen  = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 9f, ColorPrimary);

                // ── Header Banner ──────────────────────────
                var headerTable = new PdfPTable(1)
                {
                    WidthPercentage = 100
                };
                headerTable.DefaultCell.Border = 0;

                var headerCell = new PdfPCell()
                {
                    BackgroundColor = ColorDark,
                    Border          = 0,
                    Padding         = 24f
                };

                var brandFont = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 18f, ColorPrimary);
                var brandPara = new Paragraph("ESTAFF", brandFont)
                {
                    Alignment = Element.ALIGN_LEFT
                };

                var reportTypeFont = FontFactory.GetFont(
                    FontFactory.HELVETICA, 10f,
                    new BaseColor(255,255,255,160));
                var reportTypePara = new Paragraph(
                    vm.ReportType.ToString() + " Report",
                    reportTypeFont)
                {
                    Alignment = Element.ALIGN_LEFT
                };

                headerCell.AddElement(brandPara);
                headerCell.AddElement(reportTypePara);
                headerTable.AddCell(headerCell);
                doc.Add(headerTable);

                doc.Add(new Paragraph(" "));

                // ── Employee Info Row ──────────────────────
                var infoTable = new PdfPTable(2)
                {
                    WidthPercentage = 100
                };
                infoTable.SetWidths(new float[] { 1f, 1f });

                void AddInfoCell(string label, string value,
                    bool rightAlign = false)
                {
                    var cell = new PdfPCell()
                    {
                        Border          = 0,
                        BackgroundColor = ColorLight,
                        Padding         = 10f,
                        HorizontalAlignment = rightAlign
                            ? Element.ALIGN_RIGHT
                            : Element.ALIGN_LEFT
                    };

                    var lFont = FontFactory.GetFont(
                        FontFactory.HELVETICA, 7.5f, ColorGray);
                    var vFont = FontFactory.GetFont(
                        FontFactory.HELVETICA_BOLD, 10f, ColorDark);

                    cell.AddElement(new Paragraph(
                        label.ToUpper(), lFont));
                    cell.AddElement(new Paragraph(value, vFont));
                    infoTable.AddCell(cell);
                }

                AddInfoCell("Employee", vm.EmployeeName);
                AddInfoCell("Employee No.", vm.EmployeeNumber,
                    true);
                AddInfoCell("Period",
                    vm.PeriodStart.ToString("MMM dd") +
                    " – " +
                    vm.PeriodEnd.ToString("MMM dd, yyyy"));
                AddInfoCell("Report Status",
                    vm.Status.ToString(), true);

                doc.Add(infoTable);
                doc.Add(new Paragraph(" "));

                // ── Stats Row ──────────────────────────────
                var statsTable = new PdfPTable(4)
                {
                    WidthPercentage = 100
                };

                void AddStatCell(string label, string value,
                    BaseColor bg, BaseColor textColor)
                {
                    var cell = new PdfPCell()
                    {
                        Border          = 0,
                        BackgroundColor = bg,
                        Padding         = 14f,
                        HorizontalAlignment = Element.ALIGN_CENTER
                    };

                    var vFont = FontFactory.GetFont(
                        FontFactory.HELVETICA_BOLD, 18f, textColor);
                    var lFont = FontFactory.GetFont(
                        FontFactory.HELVETICA, 7.5f, ColorGray);

                    cell.AddElement(new Paragraph(value, vFont)
                    {
                        Alignment = Element.ALIGN_CENTER
                    });
                    cell.AddElement(new Paragraph(label, lFont)
                    {
                        Alignment = Element.ALIGN_CENTER
                    });
                    statsTable.AddCell(cell);
                }

                AddStatCell("Total Tasks",
                    vm.TotalTasks.ToString(),
                    ColorLight, ColorDark);
                AddStatCell("Completed",
                    vm.CompletedTasks.ToString(),
                    new BaseColor(236,253,245), ColorPrimary);
                AddStatCell("Pending",
                    vm.PendingTasks.ToString(),
                    new BaseColor(255,251,235), ColorWarning);
                AddStatCell("Overdue",
                    vm.OverdueTasks.ToString(),
                    new BaseColor(254,242,242), ColorDanger);

                doc.Add(statsTable);
                doc.Add(new Paragraph(" "));

                // Completion Rate Bar
                var rateTable = new PdfPTable(1)
                {
                    WidthPercentage = 100
                };

                var rateCell = new PdfPCell()
                {
                    Border          = 0,
                    BackgroundColor = ColorLight,
                    Padding         = 12f
                };

                var rateLabelFont = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 8f, ColorGray);
                var rateValueFont = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 11f, ColorPrimary);

                rateCell.AddElement(new Paragraph(
                    "COMPLETION RATE", rateLabelFont));
                rateCell.AddElement(new Paragraph(
                    vm.CompletionRate.ToString("F1") + "%",
                    rateValueFont));
                rateTable.AddCell(rateCell);
                doc.Add(rateTable);

                doc.Add(new Paragraph(" "));

                // ── Task Table ─────────────────────────────
                var sectionFont = FontFactory.GetFont(
                    FontFactory.HELVETICA_BOLD, 11f, ColorDark);
                doc.Add(new Paragraph("Task Details", sectionFont));
                doc.Add(new Paragraph(" ") { Leading = 6f });

                var taskTable = new PdfPTable(5)
                {
                    WidthPercentage = 100
                };
                taskTable.SetWidths(
                    new float[] { 3f, 1.2f, 1f, 1.2f, 1.2f });

                // Table header
                void AddTh(string text)
                {
                    var cell = new PdfPCell(
                        new Phrase(text, fontWhite))
                    {
                        BackgroundColor = ColorDark,
                        Border          = 0,
                        Padding         = 8f,
                        HorizontalAlignment = Element.ALIGN_LEFT
                    };
                    taskTable.AddCell(cell);
                }

                AddTh("Task Title");
                AddTh("Status");
                AddTh("Priority");
                AddTh("Due Date");
                AddTh("Completed");

                // Table rows
                bool alt = false;
                foreach (var task in vm.Tasks)
                {
                    var rowBg = alt ? ColorLight : ColorWhite;
                    alt = !alt;

                    var statusColor = task.Status switch
                    {
                        TaskStatus.Complete   => ColorPrimary,
                        TaskStatus.Overdue    => ColorDanger,
                        TaskStatus.InProgress => new BaseColor(59,130,246),
                        _                     => ColorWarning
                    };

                    void AddTd(string text,
                        BaseColor fg = null, bool bold = false)
                    {
                        var fnt = bold
                            ? FontFactory.GetFont(
                                FontFactory.HELVETICA_BOLD, 8.5f,
                                fg ?? ColorDark)
                            : FontFactory.GetFont(
                                FontFactory.HELVETICA, 8.5f,
                                fg ?? ColorGray);

                        var cell = new PdfPCell(
                            new Phrase(text, fnt))
                        {
                            BackgroundColor = rowBg,
                            Border          = 0,
                            BorderWidthBottom = 0.5f,
                            BorderColorBottom = ColorBorder,
                            Padding         = 7f
                        };
                        taskTable.AddCell(cell);
                    }

                    AddTd(task.Title, ColorDark, true);
                    AddTd(task.Status.ToString(), statusColor, true);
                    AddTd(task.Priority.HasValue
                        ? task.Priority.ToString() : "—");
                    AddTd(task.DueDate.ToString("MMM dd, yyyy"));
                    AddTd(task.CompletedDate.HasValue
                        ? task.CompletedDate.Value.ToString("MMM dd")
                        : "—");
                }

                doc.Add(taskTable);
                doc.Add(new Paragraph(" "));

                // ── Rejection Reason (if rejected) ─────────
                if (vm.Status == ReportStatus.Rejected
                    && !string.IsNullOrEmpty(vm.RejectionReason))
                {
                    var rejTable = new PdfPTable(1)
                    {
                        WidthPercentage = 100
                    };

                    var rejCell = new PdfPCell()
                    {
                        Border          = 0,
                        BackgroundColor = new BaseColor(254,242,242),
                        Padding         = 14f,
                        BorderWidthLeft = 3f,
                        BorderColorLeft = ColorDanger
                    };

                    var rejLabelFont = FontFactory.GetFont(
                        FontFactory.HELVETICA_BOLD, 8f, ColorDanger);
                    var rejTextFont  = FontFactory.GetFont(
                        FontFactory.HELVETICA, 9f, ColorDark);

                    rejCell.AddElement(new Paragraph(
                        "REJECTION REASON", rejLabelFont));
                    rejCell.AddElement(new Paragraph(
                        vm.RejectionReason, rejTextFont));
                    rejTable.AddCell(rejCell);
                    doc.Add(rejTable);
                    doc.Add(new Paragraph(" "));
                }

                // ── Footer ─────────────────────────────────
                var footerFont = FontFactory.GetFont(
                    FontFactory.HELVETICA, 7.5f, ColorGray);
                doc.Add(new Paragraph(
                    $"Generated by EStaff · " +
                    $"{DateTime.Now:MMM dd, yyyy h:mm tt}",
                    footerFont)
                {
                    Alignment = Element.ALIGN_CENTER
                });

                doc.Close();
                return ms.ToArray();
            }
        }
    }
}