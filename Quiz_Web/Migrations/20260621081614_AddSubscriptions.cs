using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quiz_Web.Migrations;

/// <summary>
/// Nâng cấp các bảng subscription đã tồn tại trên Google Cloud SQL.
/// Chỉ thêm cột/index/FK, không xóa hoặc đổi tên dữ liệu hiện hữu.
/// </summary>
public partial class AddSubscriptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "SubscriptionPlans",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "SubscriptionPlans",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.Sql("""
            UPDATE SubscriptionPlans
            SET Name = CASE DurationMonths
                WHEN 1 THEN N'Gói 1 tháng'
                WHEN 6 THEN N'Gói 6 tháng'
                WHEN 12 THEN N'Gói 12 tháng'
                ELSE CONCAT(N'Gói ', DurationMonths, N' tháng')
            END
            WHERE Name = N'';
            """);

        migrationBuilder.AddColumn<int>(
            name: "PlanId",
            table: "UserSubscriptions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Purpose",
            table: "Payments",
            type: "varchar(20)",
            unicode: false,
            maxLength: 20,
            nullable: false,
            defaultValue: "Course");

        migrationBuilder.AddColumn<int>(
            name: "SubscriptionPlanId",
            table: "Payments",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TransactionId",
            table: "Payments",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CoursePurchases_AccessCheck",
            table: "CoursePurchases",
            columns: new[] { "BuyerId", "CourseId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Payments_ProviderRef",
            table: "Payments",
            column: "ProviderRef");

        migrationBuilder.CreateIndex(
            name: "IX_Payments_SubscriptionPlanId",
            table: "Payments",
            column: "SubscriptionPlanId");

        migrationBuilder.CreateIndex(
            name: "IX_UserSubscriptions_AccessCheck",
            table: "UserSubscriptions",
            columns: new[] { "UserId", "Status", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UserSubscriptions_PlanId",
            table: "UserSubscriptions",
            column: "PlanId");

        migrationBuilder.AddForeignKey(
            name: "FK_Payments_SubscriptionPlan",
            table: "Payments",
            column: "SubscriptionPlanId",
            principalTable: "SubscriptionPlans",
            principalColumn: "PlanId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_UserSubscriptions_Plan",
            table: "UserSubscriptions",
            column: "PlanId",
            principalTable: "SubscriptionPlans",
            principalColumn: "PlanId",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_Payments_SubscriptionPlan", "Payments");
        migrationBuilder.DropForeignKey("FK_UserSubscriptions_Plan", "UserSubscriptions");
        migrationBuilder.DropIndex("IX_CoursePurchases_AccessCheck", "CoursePurchases");
        migrationBuilder.DropIndex("IX_Payments_ProviderRef", "Payments");
        migrationBuilder.DropIndex("IX_Payments_SubscriptionPlanId", "Payments");
        migrationBuilder.DropIndex("IX_UserSubscriptions_AccessCheck", "UserSubscriptions");
        migrationBuilder.DropIndex("IX_UserSubscriptions_PlanId", "UserSubscriptions");
        migrationBuilder.DropColumn("Purpose", "Payments");
        migrationBuilder.DropColumn("SubscriptionPlanId", "Payments");
        migrationBuilder.DropColumn("TransactionId", "Payments");
        migrationBuilder.DropColumn("PlanId", "UserSubscriptions");
        migrationBuilder.DropColumn("Name", "SubscriptionPlans");
        migrationBuilder.DropColumn("IsActive", "SubscriptionPlans");
    }
}
