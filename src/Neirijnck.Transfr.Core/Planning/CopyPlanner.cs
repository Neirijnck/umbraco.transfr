using System.Globalization;
using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.References;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Builds the dry run of a copy from here to a target: one row per selected document with what the copy would do and
/// why, plus the media items those documents need that the target lacks (with their folder chain), before the documents.
/// Nothing is deleted, unpublished or moved, so a plan never says so: it reports what it leaves alone instead.
/// </summary>
public static class CopyPlanner
{
    /// <exception cref="ArgumentException">
    /// A source is listed twice, a source's document type has no schema check, or a planned media item's ancestor is
    /// missing from <see cref="CopyPlanFacts.LocalMedia"/>.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">A document is not valid JSON.</exception>
    public static IReadOnlyList<PlanRow> Plan(CopyPlanFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var sources = new Dictionary<Guid, Source>();
        foreach (var document in facts.Sources)
        {
            if (!sources.TryAdd(document.Key, Read(document, facts)))
            {
                throw new ArgumentException($"The document {document.Key} is listed twice.", nameof(facts));
            }
        }

        // Blocking spreads: a blocked parent blocks the children it would have created, and a blocked document blocks
        // the documents that reference it. Rows only ever become blocked, so this settles.
        var blocked = new HashSet<Guid>();
        bool changed;
        do
        {
            changed = false;
            foreach (var source in sources.Values.Where(source => !blocked.Contains(source.Document.Key)))
            {
                if (BlockingNotes(source, sources, blocked, facts).Count > 0)
                {
                    blocked.Add(source.Document.Key);
                    changed = true;
                }
            }
        }
        while (changed);

        // Parents come before their children, so whether a parent ends up published is known when its children are planned.
        var documentRows = new List<PlanRow>();
        var publishedAfter = new Dictionary<Guid, bool>();
        foreach (var document in facts.Sources)
        {
            var row = Row(sources[document.Key], sources, blocked, facts, publishedAfter);
            documentRows.Add(row);
            publishedAfter[row.Key] = facts.PublishedOnTarget.Contains(row.Key)
                || (row.Outcome != PlanOutcome.Blocked && row.PublishCultures.Count > 0);
        }

        return [.. MediaRows(documentRows, sources, facts), .. PlaceAmongSiblings(documentRows, sources, facts)];
    }

    /// <summary>
    /// The nodes in each parent's place on the target take their order from here (<see cref="SiblingOrder"/>). A node
    /// whose predecessor changes gets a note. Skipped conflicts, blocked nodes and nodes under another parent on the
    /// target don't move (a copy never moves a node to another parent).
    /// </summary>
    private static List<PlanRow> PlaceAmongSiblings(List<PlanRow> rows, Dictionary<Guid, Source> sources, CopyPlanFacts facts)
    {
        var placed = new Dictionary<Guid, PlanNote>();
        var moving = rows
            .Where(row => row.Outcome is PlanOutcome.Create or PlanOutcome.Update or PlanOutcome.Unchanged)
            .Where(row => sources[row.Key].Target is not { } target || target.ParentKey == row.ParentKey);
        foreach (var group in moving.GroupBy(row => row.ParentKey))
        {
            var targetOrder = facts.TargetSiblings.Where(sibling => sibling.ParentKey == group.Key).Select(sibling => sibling.Key).ToList();
            var localOrder = facts.LocalSiblings.Where(sibling => sibling.ParentKey == group.Key).Select(sibling => sibling.Key).ToList();
            var newOrder = SiblingOrder.Place(targetOrder, localOrder, group.Select(row => row.Key).ToHashSet());

            // Without a sort, a created node would end up last.
            var unsorted = targetOrder.Concat(group.Where(row => row.Outcome == PlanOutcome.Create).Select(row => row.Key)).ToList();
            foreach (var row in group)
            {
                var after = SiblingOrder.PredecessorOf(newOrder, row.Key);
                if (after != SiblingOrder.PredecessorOf(unsorted, row.Key))
                {
                    placed[row.Key] = new PlanNote(PlanNoteKind.PlacedAfter, after);
                }
            }
        }

        return rows.Select(row => placed.TryGetValue(row.Key, out var note) ? row with { Notes = [.. row.Notes, note] } : row).ToList();
    }

    private static Source Read(SourceDocument document, CopyPlanFacts facts)
    {
        var json = DocumentJson.Parse(document.Json, nameof(facts));
        var documentTypeKey = DocumentJson.IdOf(json["documentType"])
            ?? throw new ArgumentException($"The document {document.Key} names no document type.", nameof(facts));
        if (!facts.Schema.TryGetValue(documentTypeKey, out var schema))
        {
            throw new ArgumentException($"No schema check for the document type {documentTypeKey} of {document.Key}.", nameof(facts));
        }

        facts.TargetDocuments.TryGetValue(document.Key, out var target);
        var targetJson = target is null ? null : DocumentJson.Parse(target.Json, nameof(facts));
        return new Source(
            document,
            json,
            documentTypeKey,
            schema,
            target,
            targetJson,
            ReferenceExtractor.Extract(document.Json),
            target is null ? null : ContentComparer.Compare(document.Json, target.Json));
    }

    // Everything that blocks a source, given the sources blocked so far.
    private static List<PlanNote> BlockingNotes(Source source, Dictionary<Guid, Source> sources, HashSet<Guid> blocked, CopyPlanFacts facts)
    {
        var notes = new List<PlanNote>();
        if (!source.Schema.IsOk)
        {
            notes.Add(new PlanNote(PlanNoteKind.SchemaMismatch, source.DocumentTypeKey));
        }

        if (source.TargetJson is { } targetJson)
        {
            if (targetJson["isTrashed"] is JsonValue trashed && trashed.TryGetValue<bool>(out var isTrashed) && isTrashed)
            {
                notes.Add(new PlanNote(PlanNoteKind.TrashedOnTarget));
            }

            if (DocumentJson.IdOf(targetJson["documentType"]) != source.DocumentTypeKey)
            {
                notes.Add(new PlanNote(PlanNoteKind.DocumentTypeDiffers, DocumentJson.IdOf(targetJson["documentType"])));
            }

            // Identical content writes nothing, so what it references is already there as it is.
            if (notes.Count > 0 || source.Comparison!.IsIdentical)
            {
                return notes;
            }
        }
        else
        {
            // A parent that is not on the target is fine when this plan creates it first.
            if (source.Document.ParentKey is { } parentKey && !facts.DocumentsOnTarget.Contains(parentKey))
            {
                if (!sources.ContainsKey(parentKey))
                {
                    notes.Add(new PlanNote(PlanNoteKind.ParentMissingOnTarget, parentKey));
                }
                else if (blocked.Contains(parentKey))
                {
                    notes.Add(new PlanNote(PlanNoteKind.ParentBlocked, parentKey));
                }
            }

            var namesake = facts.TargetSiblings.FirstOrDefault(sibling => sibling.ParentKey == source.Document.ParentKey
                && sibling.Key != source.Document.Key
                && SameName(sibling.Name, source.Document.Name));
            if (namesake is not null)
            {
                notes.Add(new PlanNote(PlanNoteKind.SameNameOtherKey, namesake.Key));
            }
        }

        foreach (var reference in source.References)
        {
            if (reference.Kind == ReferenceKind.Member)
            {
                notes.Add(new PlanNote(PlanNoteKind.MemberReference, reference.Key, reference.PropertyAlias, reference.Culture));
            }
            else if (reference.Kind == ReferenceKind.Document
                && !facts.DocumentsOnTarget.Contains(reference.Key)
                && !(sources.ContainsKey(reference.Key) && !blocked.Contains(reference.Key)))
            {
                notes.Add(new PlanNote(PlanNoteKind.MissingReference, reference.Key, reference.PropertyAlias, reference.Culture));
            }
        }

        return notes;
    }

    private static PlanRow Row(
        Source source,
        Dictionary<Guid, Source> sources,
        HashSet<Guid> blocked,
        CopyPlanFacts facts,
        Dictionary<Guid, bool> publishedAfter)
    {
        var document = source.Document;
        var differences = source.Comparison?.Differences ?? [];
        var schemaProblems = source.Schema.Problems;
        if (blocked.Contains(document.Key))
        {
            return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, PlanOutcome.Blocked,
                BlockingNotes(source, sources, blocked, facts), [], differences, schemaProblems);
        }

        var notes = new List<PlanNote>();
        if (source.Target is { } target && target.ParentKey != document.ParentKey)
        {
            notes.Add(new PlanNote(PlanNoteKind.UnderAnotherParentOnTarget, target.ParentKey));
        }

        PlanOutcome outcome;
        if (source.TargetJson is null)
        {
            outcome = PlanOutcome.Create;
        }
        else if (source.Comparison!.IsIdentical)
        {
            return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, PlanOutcome.Unchanged, notes, [], differences, schemaProblems);
        }
        else if (!facts.Baseline.TryGetValue(document.Key, out var copiedAt))
        {
            outcome = PlanOutcome.Conflict;
            notes.Add(new PlanNote(PlanNoteKind.NoBaseline));
        }
        else if (LastUpdate(source.TargetJson) == copiedAt)
        {
            outcome = PlanOutcome.Update;
        }
        else
        {
            outcome = PlanOutcome.Conflict;
            notes.Add(new PlanNote(PlanNoteKind.ChangedSinceLastCopy));
        }

        var publish = new List<string?>();
        var targetVariants = DocumentJson.Entries(source.TargetJson?["variants"], withAlias: false);
        foreach (var (key, variant) in DocumentJson.Entries(source.Json["variants"], withAlias: false))
        {
            var culture = DocumentJson.StringOf(variant["culture"]);
            switch (DocumentJson.StringOf(variant["state"]))
            {
                case "Published":
                    if (!publish.Contains(culture))
                    {
                        publish.Add(culture);
                    }

                    break;
                case "PublishedPendingChanges":
                    notes.Add(new PlanNote(PlanNoteKind.DraftOnly, Culture: culture));
                    break;
                case "Draft":
                    var targetState = DocumentJson.StringOf(targetVariants.FirstOrDefault(entry => entry.Key == key).Entry?["state"]);
                    if (targetState is "Published" or "PublishedPendingChanges")
                    {
                        notes.Add(new PlanNote(PlanNoteKind.PublishStateNotApplied, Culture: culture));
                    }

                    break;
            }
        }

        // Measured in 17.7.0: publishing under an unpublished parent fails (and create/update-and-publish then still
        // save the node as a draft, behind a 500). Plan the draft instead and say why.
        if (publish.Count > 0 && document.ParentKey is { } parentKey
            && !(publishedAfter.TryGetValue(parentKey, out var parentPublished) ? parentPublished : facts.PublishedOnTarget.Contains(parentKey)))
        {
            publish.Clear();
            notes.Add(new PlanNote(PlanNoteKind.ParentNotPublished, parentKey));
        }

        foreach (var reference in source.References.Where(reference => reference.Kind == ReferenceKind.Media
            && !facts.MediaOnTarget.Contains(reference.Key)
            && !facts.LocalMedia.ContainsKey(reference.Key)))
        {
            notes.Add(new PlanNote(PlanNoteKind.MediaMissingHere, reference.Key, reference.PropertyAlias, reference.Culture));
        }

        return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, outcome, notes, publish, differences, schemaProblems);
    }

    // The media the written documents reference that the target lacks, each after the folders it needs.
    private static List<PlanRow> MediaRows(List<PlanRow> documentRows, Dictionary<Guid, Source> sources, CopyPlanFacts facts)
    {
        var rows = new List<PlanRow>();
        var planned = new HashSet<Guid>();
        var written = documentRows.Where(row => row.Outcome is PlanOutcome.Create or PlanOutcome.Update or PlanOutcome.Conflict);
        foreach (var reference in written.SelectMany(row => sources[row.Key].References).Where(reference => reference.Kind == ReferenceKind.Media))
        {
            if (facts.MediaOnTarget.Contains(reference.Key) || !facts.LocalMedia.TryGetValue(reference.Key, out var item))
            {
                continue;
            }

            // Up to the first folder the target has (or one planned already, or the root).
            var chain = new List<LocalMediaItem>();
            var current = item;
            while (!planned.Contains(current.Key))
            {
                chain.Add(current);
                if (current.ParentKey is not { } parentKey || facts.MediaOnTarget.Contains(parentKey))
                {
                    break;
                }

                current = facts.LocalMedia.GetValueOrDefault(parentKey)
                    ?? throw new ArgumentException($"The media folder {parentKey} of {current.Key} is not given.", nameof(facts));
            }

            chain.Reverse();
            foreach (var media in chain.Where(media => planned.Add(media.Key)))
            {
                rows.Add(new PlanRow(PlanItemKind.Media, media.Key, media.ParentKey, media.Name, null, PlanOutcome.Create, [], [], [], []));
            }
        }

        return rows;
    }

    // The target's last save of any variant: what a recorded copy is checked against.
    private static DateTimeOffset? LastUpdate(JsonObject document)
        => DocumentJson.Entries(document["variants"], withAlias: false)
            .Select(variant => DateTimeOffset.TryParse(DocumentJson.StringOf(variant.Entry["updateDate"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateTimeOffset?)null)
            .Max();

    private static bool SameName(string left, string right)
        => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record Source(
        SourceDocument Document,
        JsonObject Json,
        Guid DocumentTypeKey,
        SchemaCheck Schema,
        TargetDocument? Target,
        JsonObject? TargetJson,
        IReadOnlyList<ContentReference> References,
        ContentComparison? Comparison);
}