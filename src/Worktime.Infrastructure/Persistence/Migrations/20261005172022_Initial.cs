using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Worktime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role, x.permission });
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    supervisor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_users_supervisor_id",
                        column: x => x.supervisor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    preferred_supervisor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_requests_users_preferred_supervisor_id",
                        column: x => x.preferred_supervisor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_assignment_requests_users_worker_id",
                        column: x => x.worker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "punch_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_punch_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_punch_sessions_users_worker_id",
                        column: x => x.worker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_logs_users_worker_id",
                        column: x => x.worker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_log_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_log_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_log_events_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_log_events_work_logs_work_log_id",
                        column: x => x.work_log_id,
                        principalTable: "work_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignment_requests_pending",
                table: "assignment_requests",
                column: "worker_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_requests_preferred_supervisor_id",
                table: "assignment_requests",
                column: "preferred_supervisor_id");

            migrationBuilder.CreateIndex(
                name: "ix_punch_sessions_open",
                table: "punch_sessions",
                column: "worker_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_supervisor_id",
                table: "users",
                column: "supervisor_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_log_events_actor_id",
                table: "work_log_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_log_events_work_log_id",
                table: "work_log_events",
                column: "work_log_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_logs_status",
                table: "work_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_work_logs_worker_id_start_at",
                table: "work_logs",
                columns: new[] { "worker_id", "start_at" });

            // No overlapping hours per worker, enforced by Postgres even under concurrent inserts/edits.
            // Rejected logs do not count. EF has no fluent API for EXCLUDE; needs btree_gist for uuid '='.
            migrationBuilder.Sql("""
                ALTER TABLE work_logs ADD CONSTRAINT ex_work_logs_no_overlap
                EXCLUDE USING gist (worker_id WITH =, tstzrange(start_at, end_at, '[)') WITH &&)
                WHERE (status <> 'Rejected');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE work_logs DROP CONSTRAINT IF EXISTS ex_work_logs_no_overlap;");

            migrationBuilder.DropTable(
                name: "assignment_requests");

            migrationBuilder.DropTable(
                name: "punch_sessions");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "work_log_events");

            migrationBuilder.DropTable(
                name: "work_logs");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
