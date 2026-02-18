namespace Email.Domain.Entities
{
    public sealed class EmailRequestHeader
    {
        public Guid Id { get; private set; }
        public string To { get; set; } = null!;
        public string Subject { get; set; } = null!;
        public DateTime ScheduleDateTime { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public EmailRequestDetail? Detail { get; set; }

        private EmailRequestHeader() { }

        public EmailRequestHeader(string to, string subject, DateTime scheduleDateTime)
        {
            Id = Guid.NewGuid();
            To = to;
            Subject = subject;
            ScheduleDateTime = scheduleDateTime;
            CreatedAtUtc = DateTime.UtcNow;
        }
    }
}
