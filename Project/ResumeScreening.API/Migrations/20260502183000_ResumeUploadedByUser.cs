using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResumeScreening.API.Migrations
{
    /// <inheritdoc />
    public partial class ResumeUploadedByUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UploadedByUserId",
                table: "Resumes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_JobId_UploadedByUserId",
                table: "Resumes",
                columns: new[] { "JobId", "UploadedByUserId" },
                unique: true,
                filter: "[UploadedByUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Resumes_Users_UploadedByUserId",
                table: "Resumes",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Align with EF conventions: FK index on UploadedByUserId; JobId-only index removed (composite covers JobId prefix).
            migrationBuilder.DropIndex(
                name: "IX_Resumes_JobId",
                table: "Resumes");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_UploadedByUserId",
                table: "Resumes",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resumes_UploadedByUserId",
                table: "Resumes");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_JobId",
                table: "Resumes",
                column: "JobId");

            migrationBuilder.DropForeignKey(
                name: "FK_Resumes_Users_UploadedByUserId",
                table: "Resumes");

            migrationBuilder.DropIndex(
                name: "IX_Resumes_JobId_UploadedByUserId",
                table: "Resumes");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "Resumes");
        }
    }
}
