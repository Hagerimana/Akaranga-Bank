using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AkarangaBankMvc.Web.Migrations
{
    /// <inheritdoc />
    public partial class Chapitre12_PretsEtCartes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Les produits de prêt doivent exister avant d'être référencés.
            migrationBuilder.CreateTable(
                name: "TypesPret",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MontantMin = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontantMax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DureeMinMois = table.Column<int>(type: "integer", nullable: false),
                    DureeMaxMois = table.Column<int>(type: "integer", nullable: false),
                    TauxMin = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TauxMax = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Actif = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypesPret", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TypesPret_Code",
                table: "TypesPret",
                column: "Code",
                unique: true);

            // 2. Produits par défaut (idempotent avec TypePretInitializer).
            migrationBuilder.Sql("""
                INSERT INTO "TypesPret"
                    ("Code", "Libelle", "MontantMin", "MontantMax",
                     "DureeMinMois", "DureeMaxMois", "TauxMin", "TauxMax",
                     "Actif", "DateCreation")
                VALUES
                    ('CONSO', 'Crédit à la consommation',
                     50000, 50000000, 6, 60, 10, 18, TRUE, now()),
                    ('IMMO', 'Crédit immobilier',
                     5000000, 500000000, 60, 240, 8, 14, TRUE, now()),
                    ('AUTO', 'Crédit automobile',
                     1000000, 80000000, 12, 84, 9, 16, TRUE, now()),
                    ('ENTR', 'Crédit d''entreprise',
                     1000000, 200000000, 12, 120, 10, 20, TRUE, now()),
                    ('MICRO', 'Microcrédit',
                     10000, 5000000, 1, 24, 12, 24, TRUE, now())
                ON CONFLICT ("Code") DO NOTHING;
                """);

            // 3. Colonne de liaison, alimentée depuis l'ancien libellé.
            migrationBuilder.AddColumn<int>(
                name: "TypePretId",
                table: "Prets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Prets" AS p
                SET "TypePretId" = CASE lower(p."TypePret")
                    WHEN 'immobilier' THEN
                        (SELECT "Id" FROM "TypesPret" WHERE "Code" = 'IMMO')
                    WHEN 'entreprise' THEN
                        (SELECT "Id" FROM "TypesPret" WHERE "Code" = 'ENTR')
                    ELSE
                        (SELECT "Id" FROM "TypesPret" WHERE "Code" = 'CONSO')
                END;
                """);

            migrationBuilder.CreateTable(
                name: "TransactionsCartes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CarteId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TypeTransaction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Montant = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Commercant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Autorisee"),
                    MotifRefus = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DateTransaction = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    OperationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionsCartes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionsCartes_Cartes_CarteId",
                        column: x => x.CarteId,
                        principalTable: "Cartes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionsCartes_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prets_TypePretId",
                table: "Prets",
                column: "TypePretId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsCartes_CarteId",
                table: "TransactionsCartes",
                column: "CarteId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsCartes_DateTransaction",
                table: "TransactionsCartes",
                column: "DateTransaction");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsCartes_OperationId",
                table: "TransactionsCartes",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsCartes_Reference",
                table: "TransactionsCartes",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionsCartes_Statut",
                table: "TransactionsCartes",
                column: "Statut");

            migrationBuilder.AddForeignKey(
                name: "FK_Prets_TypesPret_TypePretId",
                table: "Prets",
                column: "TypePretId",
                principalTable: "TypesPret",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. L'ancien libellé libre est remplacé par la clé étrangère.
            migrationBuilder.DropColumn(
                name: "TypePret",
                table: "Prets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prets_TypesPret_TypePretId",
                table: "Prets");

            migrationBuilder.DropTable(
                name: "TransactionsCartes");

            migrationBuilder.DropTable(
                name: "TypesPret");

            migrationBuilder.DropIndex(
                name: "IX_Prets_TypePretId",
                table: "Prets");

            migrationBuilder.DropColumn(
                name: "TypePretId",
                table: "Prets");

            migrationBuilder.AddColumn<string>(
                name: "TypePret",
                table: "Prets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Consommation");
        }
    }
}
