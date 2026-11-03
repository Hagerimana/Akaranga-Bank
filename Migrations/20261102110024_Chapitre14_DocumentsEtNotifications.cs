using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AkarangaBankMvc.Web.Migrations
{
    /// <inheritdoc />
    public partial class Chapitre14_DocumentsEtNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NomFichierOriginal = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NomFichierStocke = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CheminRelatif = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TailleOctets = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Categorie = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClientId = table.Column<int>(type: "integer", nullable: true),
                    CreeParUtilisateurId = table.Column<int>(type: "integer", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Utilisateurs_CreeParUtilisateurId",
                        column: x => x.CreeParUtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DestinataireId = table.Column<int>(type: "integer", nullable: true),
                    DestinataireEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DestinataireTelephone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Titre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NombreTentatives = table.Column<int>(type: "integer", nullable: false),
                    DerniereErreur = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Utilisateurs_DestinataireId",
                        column: x => x.DestinataireId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Langue = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Sujet = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContenuHtml = table.Column<string>(type: "text", nullable: false),
                    ContenuTexte = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentsAcces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    UtilisateurId = table.Column<int>(type: "integer", nullable: true),
                    TypeAcces = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateAcces = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdresseIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentsAcces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentsAcces_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentsAcces_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Categorie",
                table: "Documents",
                column: "Categorie");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ClientId",
                table: "Documents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreeParUtilisateurId",
                table: "Documents",
                column: "CreeParUtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Sha256Hash",
                table: "Documents",
                column: "Sha256Hash");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Statut",
                table: "Documents",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsAcces_DocumentId",
                table: "DocumentsAcces",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentsAcces_UtilisateurId",
                table: "DocumentsAcces",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DateCreation",
                table: "Notifications",
                column: "DateCreation");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DestinataireId",
                table: "Notifications",
                column: "DestinataireId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Statut",
                table: "Notifications",
                column: "Statut");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_Code_Canal_Langue",
                table: "NotificationTemplates",
                columns: new[] { "Code", "Canal", "Langue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentsAcces");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "Documents");
        }
    }
}
