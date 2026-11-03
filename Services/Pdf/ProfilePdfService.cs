using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AkarangaBankMvc.Web.Services.Pdf;

/// <summary>
/// Génère la fiche "Informations personnelles" au format PDF avec QuestPDF.
/// </summary>
public class ProfilePdfService : IProfilePdfService
{
    private static readonly string[] Mois =
    {
        "janvier", "février", "mars", "avril", "mai", "juin",
        "juillet", "août", "septembre", "octobre", "novembre", "décembre"
    };

    public byte[] Generer(ProfilePdfModel model)
    {
        return Document.Create(conteneur =>
        {
            conteneur.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#1f2937"));

                page.Header().Column(entete =>
                {
                    entete.Item().Row(ligne =>
                    {
                        ligne.RelativeItem().Column(marque =>
                        {
                            marque.Item().Text("AKARANGA BANK")
                                .FontSize(18).Bold().FontColor("#0b3d66");
                            marque.Item().Text("Espace interne — gestion bancaire")
                                .FontSize(9).FontColor("#6b7280");
                        });

                        ligne.ConstantItem(150).AlignRight().Column(date =>
                        {
                            date.Item().Text("Document confidentiel")
                                .FontSize(9).SemiBold().FontColor("#b45309");
                            date.Item().Text(FormaterDate(model.GenereLe))
                                .FontSize(9).FontColor("#6b7280");
                        });
                    });

                    entete.Item().PaddingTop(10).LineHorizontal(2).LineColor("#0b3d66");
                });

                page.Content().PaddingVertical(18).Column(contenu =>
                {
                    contenu.Item().Text("Informations personnelles")
                        .FontSize(16).SemiBold().FontColor("#0b3d66");

                    contenu.Item().PaddingTop(4).Text(
                            "Fiche générée à la demande de l'utilisateur connecté.")
                        .FontSize(9).Italic().FontColor("#6b7280");

                    contenu.Item().PaddingTop(14).Table(table =>
                    {
                        table.ColumnsDefinition(colonnes =>
                        {
                            colonnes.ConstantColumn(155);
                            colonnes.RelativeColumn();
                        });

                        Ligne(table, "Nom complet", model.NomComplet);
                        Ligne(table, "Adresse email", model.Email);
                        Ligne(table, "Email vérifié", model.EmailConfirme ? "Oui" : "Non");
                        Ligne(table, "Téléphone", model.Telephone);
                        Ligne(table, "Rôle métier", model.RoleMetier);
                        Ligne(table, "Rôles applicatifs", model.RolesIdentity);
                        Ligne(table, "Agence", model.Agence);
                        Ligne(table, "Client lié", model.ClientLie);
                        Ligne(table, "Compte créé le", FormaterDate(model.DateCreation));
                        Ligne(table, "Dernière connexion", model.DerniereConnexion is null
                            ? "—"
                            : FormaterDate(model.DerniereConnexion.Value));
                        Ligne(table, "Double authentification (2FA)", model.DeuxFacteurs ? "Activée" : "Désactivée");
                    });

                    contenu.Item().PaddingTop(18).Background("#f8fafc")
                        .Border(1).BorderColor("#e5e7eb").Padding(12)
                        .Column(note =>
                        {
                            note.Item().Text("Confidentialité").SemiBold().FontSize(10);
                            note.Item().PaddingTop(4).Text(
                                    "Ce document contient des données personnelles. Il est destiné " +
                                    "exclusivement à son titulaire et ne doit pas être partagé.")
                                .FontSize(9).FontColor("#4b5563");
                        });
                });

                page.Footer().AlignCenter().Text(texte =>
                {
                    texte.DefaultTextStyle(x => x.FontSize(8).FontColor("#9ca3af"));
                    texte.Span("Akaranga Bank · ");
                    texte.Span("Page ");
                    texte.CurrentPageNumber();
                    texte.Span(" / ");
                    texte.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Ligne(TableDescriptor table, string libelle, string? valeur)
    {
        table.Cell().Background("#f1f5f9").PaddingVertical(6).PaddingHorizontal(8)
            .Text(libelle).SemiBold().FontSize(9.5f);

        table.Cell().BorderBottom(1).BorderColor("#e5e7eb")
            .PaddingVertical(6).PaddingHorizontal(8)
            .Text(string.IsNullOrWhiteSpace(valeur) ? "—" : valeur).FontSize(9.5f);
    }

    private static string FormaterDate(DateTime date)
    {
        var local = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;

        return $"{local.Day} {Mois[local.Month - 1]} {local.Year} à {local:HH:mm}";
    }
}
