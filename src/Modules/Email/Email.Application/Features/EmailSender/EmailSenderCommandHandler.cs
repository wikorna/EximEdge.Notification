using EximEdge.Notification.Abstractions.Messaging;
using EximEdge.Notification.Contracts.Email;
using MediatR;

namespace Email.Application.Features.EmailSender
{
    public sealed class EmailSenderCommandHandler(IEventBus eventBus) : IRequestHandler<EmailSenderCommand, Guid>
    {
        public async Task<Guid> Handle(EmailSenderCommand request, CancellationToken cancellationToken)
        {
            var jobId = Guid.NewGuid();

            var message = new SendEmailMessage
            {
                JobId = jobId,
                To = request.To,
                Subject = request.Subject,
                Body = request.Body,
                CreatedAtUtc = DateTime.UtcNow
            };

            await eventBus.PublishAsync(message, cancellationToken);

            return jobId;
        }
    }
}
