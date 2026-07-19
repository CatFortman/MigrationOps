using MigrationOps.Core.Models;

/// <summary>
/// Renders a DryRunPlan to the console and reports overall success. The run fails (exit 1)
/// on any ValidationError, Changed, or VerifyFailed entry — Changed is deliberate: catching
/// a forbidden edit of an applied migration before a real run re-executes it is dry-run's
/// primary safety job, and a warning that exits 0 would sail through CI.
/// </summary>
static class DryRunReportRenderer
{
    public static bool Render(DryRunPlan plan, bool verifyRan)
    {
        Console.WriteLine($"Dry-run against {plan.TargetDatabases.Count} database(s): {string.Join(", ", plan.TargetDatabases)}");

        var groups = plan.TargetDatabases.ToList();
        if (plan.Entries.Any(e => e.Database == "(unresolved)"))
        {
            groups.Add("(unresolved)");
        }

        var nameWidth = plan.Entries.Count == 0 ? 0 : plan.Entries.Max(e => e.FileName.Length) + 2;

        foreach (var database in groups)
        {
            var entries = plan.Entries
                .Where(e => e.Database.Equals(database, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine();
            Console.WriteLine($"{database}:");

            if (entries.Count == 0)
            {
                Console.WriteLine("  (nothing to do)");
                continue;
            }

            foreach (var entry in entries)
            {
                RenderEntry(entry, nameWidth);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Summary:");

        foreach (var database in groups)
        {
            var entries = plan.Entries
                .Where(e => e.Database.Equals(database, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var counts =
                $"{entries.Count(e => e.Status == PlanEntryStatus.AlreadyApplied)} applied, " +
                $"{entries.Count(e => e.Status == PlanEntryStatus.WouldApply)} pending, " +
                $"{entries.Count(e => e.Status == PlanEntryStatus.Changed)} changed, " +
                $"{entries.Count(e => e.Status == PlanEntryStatus.ValidationError)} errors";

            if (verifyRan && entries.Any(e => e.VerifyStatus != null))
            {
                counts +=
                    $"   (verify: {entries.Count(e => e.VerifyStatus == PlanEntryStatus.VerifyPassed)} passed, " +
                    $"{entries.Count(e => e.VerifyStatus == PlanEntryStatus.VerifyFailed)} failed, " +
                    $"{entries.Count(e => e.VerifyStatus == PlanEntryStatus.NotVerified)} not verified)";
            }

            Console.WriteLine($"  {database}: {counts}");
        }

        var succeeded =
            !plan.Entries.Any(e => e.Status == PlanEntryStatus.ValidationError
                                || e.Status == PlanEntryStatus.Changed
                                || e.VerifyStatus == PlanEntryStatus.VerifyFailed);

        Console.WriteLine();
        Console.WriteLine(succeeded ? "DRY-RUN SUCCEEDED" : "DRY-RUN FAILED");

        return succeeded;
    }

    private static void RenderEntry(PlanEntry entry, int nameWidth)
    {
        var (marker, description) = entry.Status switch
        {
            PlanEntryStatus.AlreadyApplied => ("=", "already applied"),
            PlanEntryStatus.WouldApply => ("+", entry.Detail ?? "would apply"),
            PlanEntryStatus.Changed => ("~", $"CHANGED: {entry.Detail}"),
            _ => ("x", $"ERROR: {entry.Detail}"),
        };

        var line = $"  {marker} {entry.FileName.PadRight(nameWidth)}{description}";

        if (entry.VerifyStatus != null)
        {
            line += entry.VerifyStatus switch
            {
                PlanEntryStatus.VerifyPassed => "   [verify: PASSED]",
                PlanEntryStatus.VerifyFailed => $"   [verify: FAILED: {entry.VerifyDetail}]",
                _ => $"   [verify: {entry.VerifyDetail}]",
            };
        }

        Console.WriteLine(line);

        if (entry.Status == PlanEntryStatus.Changed && entry.Kind == ScriptKind.Migration)
        {
            Console.WriteLine("      WARNING: a real run would RE-EXECUTE this migration. Applied migrations must");
            Console.WriteLine("      never be edited; put the fix in a new migration.");
        }
    }
}
