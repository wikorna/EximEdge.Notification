using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Domain.Entities
{
    public sealed class EmailRequestHeader
    {
        public DateTime ScheduleDateTime {  get; set; }
        public string Subject { get; set; }
        public EmailRequestHeader(string subject, DateTime scheduleDateTime)
        {
            Subject = subject;
            ScheduleDateTime = scheduleDateTime;
        }

    }
}
