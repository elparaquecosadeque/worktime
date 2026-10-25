using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worktime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Single-tenant data was demo data only (reset every 24 h): start empty and let DatabaseInitializer
            // seed the demo tenants, instead of backfilling a tenant_id for rows that are about to be replaced.
            migrationBuilder.Sql("TRUNCATE work_log_events, work_logs, punch_sessions, assignment_requests, role_permissions, users CASCADE;");

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "work_logs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "work_log_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "role_permissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "punch_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "assignment_requests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions",
                columns: new[] { "tenant_id", "role", "permission" });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(54)", maxLength: 54, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_logs_tenant_id",
                table: "work_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_log_events_tenant_id",
                table: "work_log_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_punch_sessions_tenant_id",
                table: "punch_sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_requests_tenant_id",
                table: "assignment_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_assignment_requests_tenants_tenant_id",
                table: "assignment_requests",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_punch_sessions_tenants_tenant_id",
                table: "punch_sessions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_tenants_tenant_id",
                table: "role_permissions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_work_log_events_tenants_tenant_id",
                table: "work_log_events",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_work_logs_tenants_tenant_id",
                table: "work_logs",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assignment_requests_tenants_tenant_id",
                table: "assignment_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_punch_sessions_tenants_tenant_id",
                table: "punch_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_tenants_tenant_id",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_work_log_events_tenants_tenant_id",
                table: "work_log_events");

            migrationBuilder.DropForeignKey(
                name: "fk_work_logs_tenants_tenant_id",
                table: "work_logs");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_work_logs_tenant_id",
                table: "work_logs");

            migrationBuilder.DropIndex(
                name: "ix_work_log_events_tenant_id",
                table: "work_log_events");

            migrationBuilder.DropIndex(
                name: "ix_users_email",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions");

            migrationBuilder.DropIndex(
                name: "ix_punch_sessions_tenant_id",
                table: "punch_sessions");

            migrationBuilder.DropIndex(
                name: "ix_assignment_requests_tenant_id",
                table: "assignment_requests");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "work_logs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "work_log_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "punch_sessions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "assignment_requests");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions",
                columns: new[] { "role", "permission" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }
    }
}
