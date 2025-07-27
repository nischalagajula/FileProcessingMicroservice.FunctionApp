using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FileProcessingMicroservice.FunctionApp.Models;
using global::FileProcessingMicroservice.FunctionApp.Models;
using Microsoft.EntityFrameworkCore;


namespace FileProcessingMicroservice.FunctionApp.Data
{
    
    public class FileProcessingDbContext : DbContext
    {
        public FileProcessingDbContext(DbContextOptions<FileProcessingDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProcessedFile> ProcessedFiles { get; set; }
        public DbSet<ProcessingLog> ProcessingLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ProcessedFile
            modelBuilder.Entity<ProcessedFile>(entity =>
            {
                entity.HasIndex(e => e.CorrelationId)
                      .IsUnique();

                entity.HasIndex(e => e.OriginalFileName);

                entity.HasIndex(e => e.Status);

                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");
            });

            // Configure ProcessingLog
            modelBuilder.Entity<ProcessingLog>(entity =>
            {
                entity.HasIndex(e => e.CorrelationId);

                entity.HasIndex(e => e.EventType);

                entity.HasIndex(e => e.Timestamp);

                entity.Property(e => e.Timestamp)
                      .HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}
