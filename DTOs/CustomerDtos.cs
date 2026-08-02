namespace CpPrinting.Api.DTOs
{
    public class CustomerSaveRequestDto
    {
        public string CustomerName { get; set; } =
            string.Empty;

        public string CustomerCode { get; set; } =
            string.Empty;

        public string Address { get; set; } =
            string.Empty;

        public string TinNumber { get; set; } =
            string.Empty;

        public string TelephoneNumber { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;
    }

    public class CustomerResponseDto
    {
        public string Id { get; set; } =
            string.Empty;

        public string CustomerName { get; set; } =
            string.Empty;

        public string CustomerCode { get; set; } =
            string.Empty;

        public string Address { get; set; } =
            string.Empty;

        public string TinNumber { get; set; } =
            string.Empty;

        public string TelephoneNumber { get; set; } =
            string.Empty;

        public string Email { get; set; } =
            string.Empty;

        public string CreatedBy { get; set; } =
            string.Empty;

        public DateTime CreatedAt { get; set; }

        public string UpdatedBy { get; set; } =
            string.Empty;

        public DateTime? UpdatedAt { get; set; }
    }
}