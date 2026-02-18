namespace Email.Domain.Entities
{
    public sealed class EmailRequestDetail
    {
        public Guid Id { get; private set; }
        public Guid EmailRequestHeaderId { get; set; }
        public string Body { get; set; } = null!;

        public EmailRequestHeader? Header { get; set; }

        private EmailRequestDetail() { }

        public EmailRequestDetail(Guid emailRequestHeaderId, string body)
        {
            Id = Guid.NewGuid();
            EmailRequestHeaderId = emailRequestHeaderId;
            Body = body;
        }
    }
}
