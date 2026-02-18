using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Application.Features.EmailSender
{
    public sealed class EmailSenderCommandValidator : AbstractValidator<EmailSenderCommand>
    {
        public EmailSenderCommandValidator()
        {
            RuleFor(x => x.To)
                .NotEmpty().WithMessage("Recipient email address is required.")
                .EmailAddress().WithMessage("Invalid email address format.");
            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("Email subject is required.");
            RuleFor(x => x.Body)
                .NotEmpty().WithMessage("Email body is required."); 
        }
    }
}
