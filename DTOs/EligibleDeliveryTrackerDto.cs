namespace CpPrinting.Api.DTOs
{
    public class DeliveryTrackerSizeData
    {
        public string Size { get; set; } = string.Empty;
        public int Qty { get; set; }
        public int Pd { get; set; }
        public int Fd { get; set; }
    }

    public class DeliveryTrackerRowDto
    {
        public string InDate { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string Colour { get; set; } = string.Empty;
        public string InAd { get; set; } = string.Empty;      // AD number from advice note
        public string Ad { get; set; } = string.Empty;
        public string ScheduleNo { get; set; } = string.Empty;
        public int FpoQty { get; set; }                        // Cut qty (total pieces in this AD)
        public int AllowedPd { get; set; }                     // Calculated: 10% of FpoQty
        public string CutNo { get; set; } = string.Empty;

        // Per-size breakdown
        public List<DeliveryTrackerSizeData> SizeBreakdown { get; set; } = new();

        // Totals for this row
        public int TotalQty { get; set; }
        public int SizePdTotal { get; set; }
        public int FdTotal { get; set; }
        public int Exceeded { get; set; }                      // SizePdTotal - AllowedPd (if > 0)
    }

    public class DeliveryTrackerSummaryDto
    {
        public string StoreInRecordId { get; set; } = string.Empty;
        public string StyleNo { get; set; } = string.Empty;
        public string FpoNo { get; set; } = string.Empty;      // Schedule No
        public string CustomerName { get; set; } = string.Empty;
        public int OrderQty { get; set; }                       // Bulk qty
        public int ReceivedQty { get; set; }                    // Total IN qty
        public int DeliveredQty { get; set; }                   // Total dispatched qty
        public int BalanceToRec { get; set; }                   // OrderQty - ReceivedQty
        public int PdTotal { get; set; }
        public string PdPercentage { get; set; } = "0.00";

        public List<string> AllSizes { get; set; } = new();     // All sizes across all rows
        public List<DeliveryTrackerRowDto> Rows { get; set; } = new();

        // Column totals
        public List<DeliveryTrackerSizeData> SizeTotals { get; set; } = new();
        public int GrandTotalQty { get; set; }
        public int GrandPdTotal { get; set; }
        public int GrandFdTotal { get; set; }
    }
}