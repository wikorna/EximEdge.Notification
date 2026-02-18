using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Domain.Entities
{
    public sealed class EmailRequestDetail
    {
        public string Body { get; set; }
        public EmailRequestDetail(string body)
        {
            Body = body;
        }
    }
}
