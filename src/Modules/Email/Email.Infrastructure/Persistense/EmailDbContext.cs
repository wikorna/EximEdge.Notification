using Email.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Email.Infrastructure.Persistense
{
    public class EmailDbContext : DbContext
    {
        public EmailDbContext(DbContextOptions<EmailDbContext> options): base(options)
        {
            
        }
        public DbSet<EmailRequestHeader> EmailRequestHeaders { get; set; }
        public DbSet<EmailRequestDetail> EmailRequestDetails { get; set; }
    }
}
