using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AkarangaBankMvc.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuthentificationEmailEtCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodesVerification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UtilisateurId = table.Column<int>(type: "integer", nullable: false),
                    CodeProtege = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Objectif = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UtiliseLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tentatives = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodesVerification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodesVerification_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodesVerification_ExpireLe",
                table: "CodesVerification",
                column: "ExpireLe");

            migrationBuilder.CreateIndex(
                name: "IX_CodesVerification_UtilisateurId_Objectif_UtiliseLe",
                table: "CodesVerification",
                columns: new[] { "UtilisateurId", "Objectif", "UtiliseLe" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodesVerification");
        }
    }
}
