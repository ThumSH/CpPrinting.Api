using System.ComponentModel.DataAnnotations;

namespace CpPrinting.Api.Models
{
    /// <summary>
    /// One defect row within a cut's inspection.
    /// 14 fixed defect types (F1-F13 + Other), one row per defect per cut.
    /// </summary>
    public class CpiDefectRow
    {
        public string DefectCode { get; set; } = string.Empty; // F1, F2, ... F13, Other
        public string DefectName { get; set; } = string.Empty;

        // Before printing process
        public double BeforeLength { get; set; }
        public double BeforeWidth { get; set; }

        // After printing process
        public double AfterLength { get; set; }
        public double AfterWidth { get; set; }

        // Calculated: sum of all 4 measurements
        public double DefectedQty { get; set; }
        public string Percentage { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    /// <summary>
    /// Inspection data for one cut within the CPI report.
    /// Each cut has its own set of 14 defect rows + bundle info (auto-filled) + part selection.
    /// </summary>
    public class CpiCutInspection
    {
        public string CutRecordId { get; set; } = string.Empty;
        public string CutNo { get; set; } = string.Empty;
        public int CutQty { get; set; }

        // Bundle info (auto-filled from Store-In child tables)
        public string BundleNos { get; set; } = string.Empty;  // Comma-joined bundle numbers
        public string Sizes { get; set; } = string.Empty;       // Comma-joined sizes
        public string NumberRanges { get; set; } = string.Empty; // Comma-joined ranges

        // Part (component) — selected by user from available components
        public string Part { get; set; } = string.Empty;

        // Auto-calculated: 10% of cut qty, rounded up
        public int SampleSize { get; set; }

        // The 14 defect rows for this cut
        public List<CpiDefectRow> DefectRows { get; set; } = new();

        // Per-cut totals
        public double TotalDefectedQty { get; set; }
        public string TotalPercentage { get; set; } = string.Empty;
    }

    /// <summary>
    /// The main CPI report. One report per Store-In record.
    /// Contains multiple CpiCutInspection entries (one per cut inspected).
    /// </summary>
    public class CPIReport
    {
        [Key]
        public string Id { get; set; } = string.Empty;

        public string StoreInRecordId { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public int RevisionNo { get; set; } = 1;

        // Header — auto-populated from Store-In
        public string Date { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public string BodyColour { get; set; } = string.Empty;
        public string PrintColour { get; set; } = string.Empty;
        public int ReceivedQty { get; set; }
        public int CpiQty { get; set; }

        // Per-cut inspection data (stored as JSON)
        public List<CpiCutInspection> CutInspections { get; set; } = new();

        // Footer summary
        public int CuttingQty { get; set; }
        public int CheckedQty { get; set; }
        public int RejDamageQty { get; set; }
        public string RejectionPercentage { get; set; } = string.Empty;
        public int BalanceQty { get; set; }

        // QC gate
        public string InspectionStatus { get; set; } = "Pending"; // Pending, Passed, Failed
        public string AppRej { get; set; } = string.Empty;
        public string CheckedBy { get; set; } = string.Empty;
        public string SummaryDate { get; set; } = string.Empty;

        // CPI Auditor name
        public string CpiAuditor { get; set; } = string.Empty;
    }
}