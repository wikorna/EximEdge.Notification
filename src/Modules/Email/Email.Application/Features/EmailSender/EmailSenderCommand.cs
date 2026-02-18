using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Application.Features.EmailSender
{
    public sealed record EmailSenderCommand(string To, string Subject, string Body) : IRequest<Guid>
    {
    }
}
