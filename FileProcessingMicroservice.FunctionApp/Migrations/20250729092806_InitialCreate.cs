using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileProcessingMicroservice.FunctionApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProcessedFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProcessorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ProcessedFileSize = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LogLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AdditionalData = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_CorrelationId",
                table: "ProcessedFiles",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_CreatedAt",
                table: "ProcessedFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_OriginalFileName",
                table: "ProcessedFiles",
                column: "OriginalFileName");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_Status",
                table: "ProcessedFiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_CorrelationId",
                table: "ProcessingLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_EventType",
                table: "ProcessingLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Timestamp",
                table: "ProcessingLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedFiles");

            migrationBuilder.DropTable(
                name: "ProcessingLogs");
        }
    }
}
