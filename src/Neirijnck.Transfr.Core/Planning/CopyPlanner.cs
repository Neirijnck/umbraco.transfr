using System.Text.Json.Nodes;
using Neirijnck.Transfr.Core.Content;
using Neirijnck.Transfr.Core.References;
using Neirijnck.Transfr.Core.Schema;

namespace Neirijnck.Transfr.Core.Planning;

/// <summary>
/// Builds the dry run of a copy from here to a target: one row per selected document with what the copy would do and
/// why, plus one per media item the written documents reference (after the folders the target lacks for it), before the
/// documents. Nothing is deleted, unpublished or moved, so a plan never says so: it reports what it leaves alone instead.
/// </summary>
public static class CopyPlanner
{
    /// <exception cref="ArgumentException">
    /// A source is listed twice, a source's document type or a planned media item's media type has no schema check, a
    /// planned media item's ancestor is missing from <see cref="CopyPlanFacts.LocalMedia"/>, or a referenced media item
    /// on the target comes without its JSON.
    /// </exception>
    /// <exception cref="System.Text.Json.JsonException">A document or media item is not valid JSON.</exception>
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
        // A field-level copy only changes fields, never the node's place.
        var moving = rows
            .Where(row => row.Fields is null)
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

        // A field-level copy only carries its fields, so only what they reference can block it or needs media.
        var references = ReferenceExtractor.Extract(document.Json);
        if (document.Fields is { } fields)
        {
            references = [.. references.Where(reference => fields.Any(field => field.Covers(reference)))];
        }

        return new Source(
            document,
            json,
            documentTypeKey,
            schema,
            target,
            targetJson,
            references,
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
            if (notes.Count > 0 || IsIdentical(source))
            {
                return notes;
            }
        }
        else if (source.Document.Fields is not null)
        {
            notes.Add(new PlanNote(PlanNoteKind.NotOnTarget));
            return notes;
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
        var differences = Differences(source);
        var schemaProblems = source.Schema.Problems;
        if (blocked.Contains(document.Key))
        {
            return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, PlanOutcome.Blocked,
                BlockingNotes(source, sources, blocked, facts), [], differences, schemaProblems, Fingerprint(source), document.Fields);
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
        else if (IsIdentical(source))
        {
            return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, PlanOutcome.Unchanged, notes, [], differences, schemaProblems, Fingerprint(source), document.Fields);
        }
        else if (!facts.Baseline.TryGetValue(document.Key, out var copiedAt))
        {
            outcome = PlanOutcome.Conflict;
            notes.Add(new PlanNote(PlanNoteKind.NoBaseline));
        }
        else if (DocumentJson.LastUpdate(source.TargetJson) == copiedAt)
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
        if (document.Fields is not null)
        {
            publish.AddRange(FieldPublish(source, differences, notes));
        }

        foreach (var (key, variant) in document.Fields is null ? DocumentJson.Entries(source.Json["variants"], withAlias: false) : [])
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
            && !IsOnTarget(reference.Key, facts)
            && !facts.LocalMedia.ContainsKey(reference.Key)))
        {
            notes.Add(new PlanNote(PlanNoteKind.MediaMissingHere, reference.Key, reference.PropertyAlias, reference.Culture));
        }

        return new PlanRow(PlanItemKind.Document, document.Key, document.ParentKey, document.Name, source.DocumentTypeKey, outcome, notes, publish, differences, schemaProblems, Fingerprint(source), document.Fields);
    }

    /// <summary>
    /// The cultures a field-level copy publishes. It goes live only where no draft goes with it: a culture is published
    /// when it is published without pending changes on both sides; otherwise its fields are saved only, with a note.
    /// The cultures are those of the differing fields; an invariant field of culture-variant content goes live with
    /// every culture.
    /// </summary>
    private static List<string?> FieldPublish(Source source, IReadOnlyList<ContentDifference> differences, List<PlanNote> notes)
    {
        var cultures = DocumentJson.Entries(source.Json["variants"], withAlias: false)
            .Select(entry => DocumentJson.StringOf(entry.Entry["culture"]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var variesByCulture = cultures.Any(culture => culture is not null);
        var reached = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var difference in differences)
        {
            if (!variesByCulture || difference.Culture is null)
            {
                reached.UnionWith(cultures);
            }
            else
            {
                reached.Add(difference.Culture);
            }
        }

        var publish = new List<string?>();
        foreach (var culture in cultures.Where(reached.Contains))
        {
            switch (StateOf(source.TargetJson, culture))
            {
                case "PublishedPendingChanges":
                    notes.Add(new PlanNote(PlanNoteKind.TargetHasDraft, Culture: culture));
                    break;
                case "Published" when StateOf(source.Json, culture) == "Published":
                    publish.Add(culture);
                    break;
                case "Published":
                    notes.Add(new PlanNote(PlanNoteKind.DraftOnly, Culture: culture));
                    break;
            }
        }

        return publish;
    }

    // The state of a document's variant in a culture (no segment); null when the document or the variant is missing.
    private static string? StateOf(JsonObject? json, string? culture)
        => DocumentJson.StringOf(DocumentJson.Entries(json?["variants"], withAlias: false)
            .Select(entry => entry.Entry)
            .FirstOrDefault(variant => string.Equals(DocumentJson.StringOf(variant["culture"]), culture, StringComparison.OrdinalIgnoreCase)
                && DocumentJson.StringOf(variant["segment"]) is null)?["state"]);

    // The differences that count: all of them, or for a field-level copy only those in its fields.
    private static IReadOnlyList<ContentDifference> Differences(Source source)
        => source.Document.Fields is { } fields
            ? [.. (source.Comparison?.Differences ?? []).Where(difference => fields.Any(field => field.Covers(difference)))]
            : source.Comparison?.Differences ?? [];

    // Whether a copy would write nothing to the target's node (which must exist).
    private static bool IsIdentical(Source source)
        => source.Document.Fields is null ? source.Comparison!.IsIdentical : Differences(source).Count == 0;

    /// <summary>
    /// The media the written documents reference, each after the folders the target lacks for it. A media item on the
    /// target is compared like a document (unchanged, update, or a conflict against our last copy); a missing one is
    /// created. Folders the target has are left alone: a copy never renames or moves them.
    /// </summary>
    private static List<PlanRow> MediaRows(List<PlanRow> documentRows, Dictionary<Guid, Source> sources, CopyPlanFacts facts)
    {
        var rows = new List<PlanRow>();
        var planned = new Dictionary<Guid, PlanRow>();
        var written = documentRows.Where(row => row.Outcome is PlanOutcome.Create or PlanOutcome.Update or PlanOutcome.Conflict);
        var keys = written.SelectMany(row => sources[row.Key].References)
            .Where(reference => reference.Kind == ReferenceKind.Media)
            .Select(reference => reference.Key)
            .Distinct();
        foreach (var key in keys)
        {
            if (planned.ContainsKey(key) || !facts.LocalMedia.TryGetValue(key, out var item))
            {
                continue;
            }

            // Up to the first folder the target has (or one planned already, or the root). An item the target has
            // (even in its recycle bin) needs no folders: it stays where it is there.
            var chain = new List<LocalMediaItem>();
            var parentKey = facts.TargetMedia.ContainsKey(item.Key) ? null : item.ParentKey;
            while (parentKey is { } folderKey && !planned.ContainsKey(folderKey) && !IsOnTarget(folderKey, facts))
            {
                var folder = facts.LocalMedia.GetValueOrDefault(folderKey)
                    ?? throw new ArgumentException($"The media folder {folderKey} of {item.Key} is not given.", nameof(facts));
                chain.Add(folder);
                parentKey = folder.ParentKey;
            }

            chain.Reverse();
            foreach (var media in chain.Append(item))
            {
                var row = MediaRow(media, media.Key == key, planned, facts);
                planned[media.Key] = row;
                rows.Add(row);
            }
        }

        return rows;
    }

    // A referenced media item, or a folder above one that the target lacks (or holds in its recycle bin).
    private static PlanRow MediaRow(LocalMediaItem item, bool referenced, Dictionary<Guid, PlanRow> planned, CopyPlanFacts facts)
    {
        var json = DocumentJson.Parse(item.Json, nameof(facts));
        var mediaTypeKey = DocumentJson.IdOf(json["mediaType"])
            ?? throw new ArgumentException($"The media item {item.Key} names no media type.", nameof(facts));
        if (!facts.MediaSchema.TryGetValue(mediaTypeKey, out var schema))
        {
            throw new ArgumentException($"No schema check for the media type {mediaTypeKey} of {item.Key}.", nameof(facts));
        }

        var target = facts.TargetMedia.GetValueOrDefault(item.Key);
        var onTarget = target is { IsTrashed: false };
        var targetJson = onTarget && referenced
            ? DocumentJson.Parse(target!.Json ?? throw new ArgumentException($"The media item {item.Key} on the target comes without its JSON.", nameof(facts)), nameof(facts))
            : null;
        var comparison = targetJson is null ? null : MediaComparer.Compare(item.Json, target!.Json!);
        var differences = comparison?.Differences ?? [];
        var fingerprint = PlanFingerprint.Of(json, targetJson);

        var notes = new List<PlanNote>();
        if (!schema.IsOk)
        {
            notes.Add(new PlanNote(PlanNoteKind.SchemaMismatch, mediaTypeKey));
        }

        if (target is { IsTrashed: true })
        {
            notes.Add(new PlanNote(PlanNoteKind.TrashedOnTarget));
        }
        else if (targetJson is not null && DocumentJson.IdOf(targetJson["mediaType"]) != mediaTypeKey)
        {
            notes.Add(new PlanNote(PlanNoteKind.MediaTypeDiffers, DocumentJson.IdOf(targetJson["mediaType"])));
        }
        else if (!onTarget && item.ParentKey is { } parentKey && planned.TryGetValue(parentKey, out var parent) && parent.Outcome == PlanOutcome.Blocked)
        {
            notes.Add(new PlanNote(PlanNoteKind.ParentBlocked, parentKey));
        }

        if (notes.Count > 0)
        {
            return new PlanRow(PlanItemKind.Media, item.Key, item.ParentKey, item.Name, mediaTypeKey, PlanOutcome.Blocked, notes, [], differences, schema.Problems, fingerprint);
        }

        if (target is not null && target.ParentKey != item.ParentKey)
        {
            notes.Add(new PlanNote(PlanNoteKind.UnderAnotherParentOnTarget, target.ParentKey));
        }

        PlanOutcome outcome;
        if (targetJson is null)
        {
            outcome = PlanOutcome.Create;
        }
        else if (comparison!.IsIdentical)
        {
            outcome = PlanOutcome.Unchanged;
        }
        else if (!facts.Baseline.TryGetValue(item.Key, out var copiedAt))
        {
            outcome = PlanOutcome.Conflict;
            notes.Add(new PlanNote(PlanNoteKind.NoBaseline));
        }
        else if (DocumentJson.LastUpdate(targetJson) == copiedAt)
        {
            outcome = PlanOutcome.Update;
        }
        else
        {
            outcome = PlanOutcome.Conflict;
            notes.Add(new PlanNote(PlanNoteKind.ChangedSinceLastCopy));
        }

        return new PlanRow(PlanItemKind.Media, item.Key, item.ParentKey, item.Name, mediaTypeKey, outcome, notes, [], differences, schema.Problems, fingerprint);
    }

    // Whether the target has this media item or folder outside its recycle bin.
    private static bool IsOnTarget(Guid key, CopyPlanFacts facts) => facts.TargetMedia.TryGetValue(key, out var target) && !target.IsTrashed;

    private static string Fingerprint(Source source) => PlanFingerprint.Of(source.Json, source.TargetJson);

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