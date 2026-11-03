using AkarangaBankMvc.Web.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AkarangaBankMvc.Web.Services.Pdf;

public class PdfService : IPdfService
{
    private const string CouleurBleuMarine = "#1F3864";
    private const string CouleurOr = "#C9A227";
    private const string CouleurTexte = "#1f2937";
    private const string CouleurFondTable = "#f8fafc";

    private readonly AkarangaBankContext _context;

    public PdfService(AkarangaBankContext context)
    {
        _context = context;
    }

    // ============================================================
    // 1. RELEVE DE COMPTE (EF-15.1)
    // ============================================================
    public async Task<byte[]> GenererReleveComptePdfAsync(int compteId, DateTime debut, DateTime fin)
    {
        var compte = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Agence)
            .Include(c => c.TypeCompte)
            .FirstOrDefaultAsync(c => c.Id == compteId);

        if (compte is null)
        {
            throw new ArgumentException($"Compte introuvable : {compteId}");
        }

        var d = DateTime.SpecifyKind(debut, DateTimeKind.Utc);
        var f = DateTime.SpecifyKind(fin, DateTimeKind.Utc).Date.AddDays(1).AddTicks(-1);

        var ecritures = await _context.Ecritures
            .AsNoTracking()
            .Include(e => e.Operation)
            .Where(e => e.CompteId == compteId && e.DateEcriture >= d && e.DateEcriture <= f)
            .OrderBy(e => e.DateEcriture)
            .ToListAsync();

        var totalDebits = ecritures.Where(e => e.Sens == "Debit").Sum(e => e.Montant);
        var totalCredits = ecritures.Where(e => e.Sens == "Credit").Sum(e => e.Montant);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CouleurTexte));

                // En-tête officiel (EF-15.1, ENF-15.1)
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(brand =>
                        {
                            brand.Item().Text("AKARANGA BANK").FontSize(18).Bold().FontColor(CouleurBleuMarine);
                            brand.Item().Text("Société Bancaire Commerciale du Burundi").FontSize(8).FontColor("#4b5563");
                            brand.Item().Text($"Agence : {compte.Agence?.Nom ?? "Siège Principal"}").FontSize(8);
                        });

                        row.ConstantItem(220).AlignRight().Column(meta =>
                        {
                            meta.Item().Text("RELEVÉ DE COMPTE").FontSize(14).Bold().FontColor(CouleurOr);
                            meta.Item().Text($"Période : {debut:dd/MM/yyyy} au {fin:dd/MM/yyyy}").FontSize(8);
                            meta.Item().Text($"Date d'édition : {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC").FontSize(8).FontColor("#6b7280");
                        });
                    });

                    header.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(CouleurBleuMarine);

                    // Bloc client et compte
                    header.Item().PaddingVertical(10).Row(clientRow =>
                    {
                        clientRow.RelativeItem().Border(1).BorderColor("#e2e8f0").Padding(8).Column(c =>
                        {
                            c.Item().Text("Titulaire du compte :").FontSize(8).Bold().FontColor(CouleurBleuMarine);
                            c.Item().Text($"{compte.Client?.Nom} {compte.Client?.Prenom}").FontSize(10).Bold();
                            c.Item().Text($"N° Client : {compte.Client?.NumeroClient}").FontSize(8);
                            c.Item().Text($"Adresse : {compte.Client?.Adresse ?? "Bujumbura, Burundi"}").FontSize(8);
                        });

                        clientRow.ConstantItem(15);

                        clientRow.RelativeItem().Border(1).BorderColor("#e2e8f0").Padding(8).Column(c =>
                        {
                            c.Item().Text("Informations compte :").FontSize(8).Bold().FontColor(CouleurBleuMarine);
                            c.Item().Text($"N° Compte : {compte.NumeroCompte}").FontSize(10).Bold();
                            c.Item().Text($"Type : {compte.TypeCompte?.Libelle ?? "Courant"}").FontSize(8);
                            c.Item().Text($"Devise : {compte.Devise}").FontSize(8);
                            c.Item().Text($"Solde actuel : {compte.Solde:N2} {compte.Devise}").FontSize(9).Bold().FontColor(CouleurBleuMarine);
                        });
                    });
                });

                // Tableau des écritures
                page.Content().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(65); // Date
                        cols.ConstantColumn(80); // Réf
                        cols.RelativeColumn();   // Libellé
                        cols.ConstantColumn(75); // Débit
                        cols.ConstantColumn(75); // Crédit
                    });

                    // En-tête de table
                    table.Header(h =>
                    {
                        h.Cell().Background(CouleurBleuMarine).Padding(4).Text("Date").FontColor("#ffffff").Bold();
                        h.Cell().Background(CouleurBleuMarine).Padding(4).Text("Référence").FontColor("#ffffff").Bold();
                        h.Cell().Background(CouleurBleuMarine).Padding(4).Text("Description").FontColor("#ffffff").Bold();
                        h.Cell().Background(CouleurBleuMarine).Padding(4).AlignRight().Text("Débit").FontColor("#ffffff").Bold();
                        h.Cell().Background(CouleurBleuMarine).Padding(4).AlignRight().Text("Crédit").FontColor("#ffffff").Bold();
                    });

                    if (!ecritures.Any())
                    {
                        table.Cell().ColumnSpan(5).Padding(12).AlignCenter().Text("Aucun mouvement enregistré sur cette période.").Italic();
                    }

                    foreach (var e in ecritures)
                    {
                        var bg = ecritures.IndexOf(e) % 2 == 1 ? CouleurFondTable : "#ffffff";
                        table.Cell().Background(bg).Padding(4).Text(e.DateEcriture.ToString("dd/MM/yyyy"));
                        table.Cell().Background(bg).Padding(4).Text(e.Operation?.Reference ?? "—").FontSize(8);
                        table.Cell().Background(bg).Padding(4).Text(e.Libelle);
                        table.Cell().Background(bg).Padding(4).AlignRight().Text(e.Sens == "Debit" ? e.Montant.ToString("N2") : "—");
                        table.Cell().Background(bg).Padding(4).AlignRight().Text(e.Sens == "Credit" ? e.Montant.ToString("N2") : "—");
                    }

                    // Ligne des totaux
                    table.Cell().ColumnSpan(3).BorderTop(1).BorderColor(CouleurBleuMarine).Padding(4).AlignRight().Text("TOTAL DES MOUVEMENTS :").Bold();
                    table.Cell().BorderTop(1).BorderColor(CouleurBleuMarine).Padding(4).AlignRight().Text($"{totalDebits:N2}").Bold();
                    table.Cell().BorderTop(1).BorderColor(CouleurBleuMarine).Padding(4).AlignRight().Text($"{totalCredits:N2}").Bold();
                });

                // Pied de page légal et numérotation
                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.5f).LineColor("#cbd5e1");
                    footer.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Akaranga Bank — Agrément BRB n° B/2024/09 — Siège Social Boulevard Patrice Lumumba, Bujumbura").FontSize(7).FontColor("#6b7280");
                        r.ConstantItem(100).AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    // ============================================================
    // 2. ATTESTATION DE SOLDE (EF-15.2)
    // ============================================================
    public async Task<byte[]> GenererAttestationSoldePdfAsync(int compteId)
    {
        var compte = await _context.Comptes
            .AsNoTracking()
            .Include(c => c.Client)
            .Include(c => c.Agence)
            .FirstOrDefaultAsync(c => c.Id == compteId);

        if (compte is null) throw new ArgumentException($"Compte introuvable : {compteId}");

        var dateDuJour = DateTime.UtcNow;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(CouleurTexte).LineHeight(1.4f));

                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Column(b =>
                        {
                            b.Item().Text("AKARANGA BANK").FontSize(20).Bold().FontColor(CouleurBleuMarine);
                            b.Item().Text("Direction des Opérations Bancaires").FontSize(9).FontColor("#4b5563");
                            b.Item().Text($"Agence de {compte.Agence?.Nom ?? "Bujumbura"}").FontSize(9);
                        });
                        r.ConstantItem(150).AlignRight().Column(d =>
                        {
                            d.Item().Text($"Bujumbura, le {dateDuJour:dd/MM/yyyy}").FontSize(10).SemiBold();
                            d.Item().Text($"Réf : ATT-SOLDE/{compte.Id:D6}").FontSize(8).FontColor("#6b7280");
                        });
                    });
                    h.Item().PaddingTop(15).LineHorizontal(1.5f).LineColor(CouleurBleuMarine);
                });

                page.Content().PaddingVertical(25).Column(c =>
                {
                    c.Item().AlignCenter().Text("ATTESTATION DE SOLDE BANCAIRE")
                        .FontSize(16).Bold().FontColor(CouleurBleuMarine).Underline();

                    c.Item().PaddingTop(25).Text("La Direction d'Akaranga Bank certifie par la présente que :");

                    c.Item().PaddingTop(15).PaddingLeft(20).Column(details =>
                    {
                        details.Item().Text($"• Titulaire : Monsieur / Madame {compte.Client?.Nom} {compte.Client?.Prenom}");
                        details.Item().Text($"• N° d'identification client : {compte.Client?.NumeroClient}");
                        details.Item().Text($"• N° de compte bancaire : {compte.NumeroCompte}");
                        details.Item().Text($"• Devise de tenue : {compte.Devise}");
                    });

                    c.Item().PaddingTop(20).Text(text =>
                    {
                        text.Span("Est titulaire à ce jour dans nos livres d'un compte présentant un solde créditeur disponible de : ");
                        text.Span($"{compte.Solde:N2} {compte.Devise}").Bold().FontColor(CouleurBleuMarine);
                    });

                    c.Item().PaddingTop(15).Text("La présente attestation lui est délivrée à sa demande pour servir et valoir ce que de droit, sous réserve des opérations en cours de compensation ou d'apurement.");

                    // Bloc de signatures officielles
                    c.Item().PaddingTop(40).Row(sig =>
                    {
                        sig.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Le Chef d'Agence").Bold();
                            col.Item().Text("Cachet & Visa").FontSize(9).Italic();
                            col.Item().PaddingTop(40).Text("Signé numériquement").FontSize(8).FontColor("#6b7280");
                        });

                        sig.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("La Direction Générale").Bold();
                            col.Item().Text("Akaranga Bank Burundi").FontSize(9).Italic();
                            col.Item().PaddingTop(40).Text("[Sceau Officiel]").FontSize(8).FontColor(CouleurOr);
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Akaranga Bank S.A. — Agrément Banque de la République du Burundi (BRB) — Document protégé contre la falsification").FontSize(8).FontColor("#94a3b8");
            });
        }).GeneratePdf();
    }

    // ============================================================
    // 3. CONTRAT DE PRET (EF-15.3)
    // ============================================================
    public async Task<byte[]> GenererContratPretPdfAsync(int pretId)
    {
        var pret = await _context.Prets
            .AsNoTracking()
            .Include(p => p.Client)
            .Include(p => p.TypePret)
            .Include(p => p.CompteDebit)
            .Include(p => p.Echeances)
            .FirstOrDefaultAsync(p => p.Id == pretId);

        if (pret is null) throw new ArgumentException($"Prêt introuvable : {pretId}");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CouleurTexte).LineHeight(1.35f));

                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Text("AKARANGA BANK — DIRECTION DES CRÉDITS").FontSize(10).Bold().FontColor(CouleurBleuMarine);
                        r.ConstantItem(150).AlignRight().Text($"Réf : {pret.Reference}").FontSize(9).Bold();
                    });
                    h.Item().LineHorizontal(1).LineColor(CouleurBleuMarine);
                });

                page.Content().PaddingVertical(15).Column(c =>
                {
                    c.Item().AlignCenter().Text("CONTRAT DE PRÊT BANCAIRE").FontSize(15).Bold().FontColor(CouleurBleuMarine);
                    c.Item().AlignCenter().Text($"Type de prêt : {pret.TypePret?.Libelle}").FontSize(10).Italic();

                    c.Item().PaddingTop(15).Text("ENTRE LES SOUSSIGNÉS :").Bold();
                    c.Item().Text("1. AKARANGA BANK S.A., ci-après dénommée 'La Banque',");
                    c.Item().Text($"2. {pret.Client?.Nom} {pret.Client?.Prenom}, identifié sous le N° {pret.Client?.NumeroClient}, ci-après dénommé 'L'Emprunteur'.");

                    c.Item().PaddingTop(12).Text("ARTICLE 1 — OBJET DU FINANCEMENT").Bold();
                    c.Item().Text($"La Banque accorde à l'Emprunteur un crédit d'un montant principal de {pret.MontantPrincipal:N2} BIF versé sur son compte N° {pret.CompteDebit?.NumeroCompte}.");

                    c.Item().PaddingTop(8).Text("ARTICLE 2 — DURÉE ET TAUX D'INTÉRÊT").Bold();
                    c.Item().Text($"Le prêt est consenti pour une durée ferme de {pret.DureeMois} mois au taux d'intérêt annuel conventionnel de {pret.TauxInteret:N2} %.");

                    c.Item().PaddingTop(8).Text("ARTICLE 3 — MODALITÉS DE REMBOURSEMENT").Bold();
                    c.Item().Text($"Le remboursement s'effectue par mensualités constantes d'un montant estimé à {pret.Mensualite:N2} BIF prélevées automatiquement sur le compte de débit désigné.");

                    c.Item().PaddingTop(8).Text("ARTICLE 4 — PÉNALITÉS ET DÉFAUT DE PAIEMENT").Bold();
                    c.Item().Text("Tout retard d'échéance non régularisé entraîne l'application d'une pénalité de 0,5 % par mois de retard (minimum 500 BIF). En cas de 3 échéances impayées consécutives, la totalité de la dette devient immédiatement exigible.");

                    c.Item().PaddingTop(8).Text("ARTICLE 5 — JURIDICTION ET LOI APPLICABLE").Bold();
                    c.Item().Text("Le présent contrat est régi par le droit bancaire de la République du Burundi. Tout litige sera soumis au Tribunal de Commerce de Bujumbura.");

                    c.Item().PaddingTop(25).Row(signatures =>
                    {
                        signatures.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Pour l'Emprunteur").Bold();
                            col.Item().Text("(Mention manuscrite 'Lu et approuvé')").FontSize(8).Italic();
                            col.Item().PaddingTop(35).Text("Signature de l'Emprunteur").FontSize(8);
                        });

                        signatures.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("Pour Akaranga Bank").Bold();
                            col.Item().Text("Le Comité de Crédit").FontSize(8).Italic();
                            col.Item().PaddingTop(35).Text("Visa & Cachet de la Banque").FontSize(8);
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Contrat officiel généré sous contrôle d'Akaranga Bank — 1 exemplaire pour la banque, 1 pour l'emprunteur").FontSize(7).FontColor("#94a3b8");
            });
        }).GeneratePdf();
    }

    // ============================================================
    // 4. DECLARATION CNRF (EF-15.4)
    // ============================================================
    public async Task<byte[]> GenererDeclarationCnrfPdfAsync(int declarationId)
    {
        var decl = await _context.DeclarationsSoupcon
            .AsNoTracking()
            .Include(d => d.Client)
            .Include(d => d.Alerte)
                .ThenInclude(a => a.Operation)
            .Include(d => d.RedigePar)
            .FirstOrDefaultAsync(d => d.Id == declarationId);

        if (decl is null) throw new ArgumentException($"Déclaration CNRF introuvable : {declarationId}");

        var montant = decl.Alerte?.Operation?.Montant ?? 0m;
        var motif = decl.MotifSuspicion ?? decl.Contenu;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.8f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CouleurTexte));

                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Column(b =>
                        {
                            b.Item().Text("RÉPUBLIQUE DU BURUNDI").FontSize(10).Bold();
                            b.Item().Text("CELLULE NATIONALE DE RENSEIGNEMENT FINANCIER (CNRF)").FontSize(11).Bold().FontColor(CouleurBleuMarine);
                            b.Item().Text("Loi n° 1/02 du 27 janvier 2017 relative à la lutte contre le blanchiment").FontSize(7).Italic();
                        });

                        r.ConstantItem(160).AlignRight().Column(c =>
                        {
                            c.Item().Text("DÉCLARATION DE SOUPÇON").FontSize(11).Bold().FontColor("#dc2626");
                            c.Item().Text($"Réf : {decl.Reference}").FontSize(8).Bold();
                            c.Item().Text($"Date : {decl.DateCreation:dd/MM/yyyy HH:mm}").FontSize(8);
                        });
                    });
                    h.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor("#dc2626");
                });

                page.Content().PaddingVertical(15).Column(c =>
                {
                    c.Item().Border(1).BorderColor("#e2e8f0").Padding(8).Column(org =>
                    {
                        org.Item().Text("ORGANISME DÉCLARANT :").Bold().FontColor(CouleurBleuMarine);
                        org.Item().Text("Akaranga Bank S.A. — Direction de la Conformité & LBC/FT");
                        org.Item().Text($"Officier de conformité rédacteur : {decl.RedigePar?.NomComplet ?? "Officier LBC/FT"}");
                    });

                    c.Item().PaddingTop(12).Border(1).BorderColor("#e2e8f0").Padding(8).Column(cl =>
                    {
                        cl.Item().Text("IDENTIFICATION DE LA PERSONNE VISÉE :").Bold().FontColor(CouleurBleuMarine);
                        cl.Item().Text($"Nom & Prénom : {decl.Client?.Nom} {decl.Client?.Prenom}");
                        cl.Item().Text($"Identifiant bancaire : {decl.Client?.NumeroClient}");
                        cl.Item().Text($"CNI : {decl.Client?.NumeroCNI}");
                        cl.Item().Text($"Statut du compte : {decl.Client?.Statut ?? "Actif"}");
                    });

                    c.Item().PaddingTop(12).Border(1).BorderColor("#e2e8f0").Padding(8).Column(op =>
                    {
                        op.Item().Text("OPÉRATION ET MOTIF DU SOUPÇON :").Bold().FontColor(CouleurBleuMarine);
                        op.Item().Text($"Montant concerné : {montant:N2} BIF").Bold();
                        op.Item().Text($"Niveau d'alerte AML : {decl.Alerte?.Niveau ?? "Élevé"}");
                        op.Item().PaddingTop(4).Text($"Exposé des motifs de suspicion : {motif}");
                    });

                    c.Item().PaddingTop(12).Text("CONFIDENTIALITÉ STRICTE : Les informations contenues dans cette déclaration sont couvertes par le secret professionnel le plus strict conformément aux dispositions légales en vigueur.").FontSize(8).Italic().FontColor("#dc2626");

                    c.Item().PaddingTop(25).Row(s =>
                    {
                        s.RelativeItem().Text("Transmis à la CNRF via canal sécurisé officiel.");
                        s.ConstantItem(180).AlignRight().Text("Le Responsable Conformité\n[Signature électronique certifiée]").FontSize(8);
                    });
                });

                page.Footer().AlignCenter().Text("Cellule Nationale de Renseignement Financier — Document classé confidentiel").FontSize(7).FontColor("#94a3b8");
            });
        }).GeneratePdf();
    }

    // ============================================================
    // 5. REÇU D'OPERATION (EF-15.5)
    // ============================================================
    public async Task<byte[]> GenererRecuOperationPdfAsync(int operationId)
    {
        var op = await _context.Operations
            .AsNoTracking()
            .Include(o => o.Compte)
                .ThenInclude(c => c.Client)
            .Include(o => o.Virement)
                .ThenInclude(v => v!.CompteSource)
                    .ThenInclude(c => c.Client)
            .Include(o => o.Virement)
                .ThenInclude(v => v!.CompteDestination)
                    .ThenInclude(c => c.Client)
            .Include(o => o.Utilisateur)
            .FirstOrDefaultAsync(o => o.Id == operationId);

        if (op is null) throw new ArgumentException($"Opération introuvable : {operationId}");

        var compteDebit = op.Virement != null
            ? $"{op.Virement.CompteSource.NumeroCompte} ({op.Virement.CompteSource.Client?.Nom})"
            : (op.TypeOperation == "Retrait" ? $"{op.Compte.NumeroCompte} ({op.Compte.Client?.Nom})" : "Caisse Guichet");

        var compteCredit = op.Virement != null
            ? $"{op.Virement.CompteDestination.NumeroCompte} ({op.Virement.CompteDestination.Client?.Nom})"
            : (op.TypeOperation == "Depot" ? $"{op.Compte.NumeroCompte} ({op.Compte.Client?.Nom})" : "Caisse Guichet");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(CouleurTexte));

                page.Header().Row(r =>
                {
                    r.RelativeItem().Column(b =>
                    {
                        b.Item().Text("AKARANGA BANK").FontSize(14).Bold().FontColor(CouleurBleuMarine);
                        b.Item().Text("Billet de Guichet & Reçu d'Opération").FontSize(8).FontColor("#6b7280");
                    });
                    r.ConstantItem(180).AlignRight().Column(m =>
                    {
                        m.Item().Text($"RÉCÉPISSÉ : {op.Reference}").FontSize(10).Bold().FontColor(CouleurOr);
                        m.Item().Text($"Date : {op.DateOperation:dd/MM/yyyy HH:mm}").FontSize(8);
                    });
                });

                page.Content().PaddingVertical(10).Column(c =>
                {
                    c.Item().LineHorizontal(1).LineColor(CouleurBleuMarine);

                    c.Item().PaddingTop(10).Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(140);
                            cols.RelativeColumn();
                        });

                        LigneRecu(t, "Type d'opération", op.TypeOperation);
                        LigneRecu(t, "Montant", $"{op.Montant:N2} {op.Devise}");
                        LigneRecu(t, "Compte débité", compteDebit);
                        LigneRecu(t, "Compte crédité", compteCredit);
                        LigneRecu(t, "Motif / Libellé", op.Commentaire ?? "Opération au guichet");
                        LigneRecu(t, "Statut", op.Statut);
                        LigneRecu(t, "Opérateur", op.Utilisateur?.NomComplet ?? "Guichetier");
                    });

                    c.Item().PaddingTop(15).Row(sig =>
                    {
                        sig.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Signature du Client :").FontSize(8).Bold();
                            col.Item().PaddingTop(25).Text("_____________________").FontSize(8);
                        });
                        sig.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("Pour Akaranga Bank :").FontSize(8).Bold();
                            col.Item().PaddingTop(25).Text("Guichet Validé [OK]").FontSize(8).FontColor(CouleurBleuMarine);
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Merci pour votre confiance. Conservez ce reçu pour tout recours.").FontSize(7).FontColor("#94a3b8");
            });
        }).GeneratePdf();
    }

    private static void LigneRecu(TableDescriptor table, string label, string valeur)
    {
        table.Cell().Padding(3).Text(label).Bold().FontColor(CouleurBleuMarine);
        table.Cell().Padding(3).Text(valeur);
    }
}
