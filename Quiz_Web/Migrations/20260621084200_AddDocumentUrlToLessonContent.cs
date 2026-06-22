using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Web.Migrations
{
    public partial class AddDocumentUrlToLessonContent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                table: "LessonContents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                table: "LessonContents");
        }
    }
}
