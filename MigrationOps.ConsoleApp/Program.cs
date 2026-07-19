using MigrationOps.Core.MigrationFramework.Services;

class Program
{
    static int Main(string[] args)
    {
        try
        {
            var migrationService = new MigrationService();

            if (args.Length == 0)
            {
                if (Console.IsInputRedirected)
                {
                    // Existing CI/scripts invoke `dotnet run` bare and must keep deploying;
                    // only an interactive console gets the menu.
                    return RunApply(migrationService, null);
                }

                return RunInteractiveMenu(migrationService);
            }

            return RunCommandLine(migrationService, args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MigrationOps run halted: {ex.Message}");
            return 1;
        }
    }

    private static int RunCommandLine(MigrationService migrationService, string[] args)
    {
        var command = args[0].ToLowerInvariant();
        string? database = null;
        var verify = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--db":
                    if (i + 1 >= args.Length)
                    {
                        return Usage("--db requires a database name.");
                    }
                    database = args[++i];
                    break;
                case "--verify":
                    verify = true;
                    break;
                default:
                    return Usage($"Unknown option '{args[i]}'.");
            }
        }

        if (database != null)
        {
            var configured = migrationService.GetDatabaseNames();
            var canonical = configured.FirstOrDefault(n => n.Equals(database, StringComparison.OrdinalIgnoreCase));

            if (canonical == null)
            {
                Console.Error.WriteLine($"Unknown database '{database}'. Configured databases: {string.Join(", ", configured)}");
                return 1;
            }

            database = canonical;
        }

        switch (command)
        {
            case "apply":
                return verify ? Usage("--verify is only valid with dry-run.") : RunApply(migrationService, database);
            case "dry-run":
                return RunDryRun(migrationService, database, verify);
            default:
                return Usage($"Unknown command '{args[0]}'.");
        }
    }

    private static int Usage(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run -- apply   [--db <name>]");
        Console.Error.WriteLine("  dotnet run -- dry-run [--db <name>] [--verify]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Running with no arguments opens an interactive menu (or performs a full");
        Console.Error.WriteLine("apply when stdin is redirected, e.g. from CI).");
        return 1;
    }

    private static int RunInteractiveMenu(MigrationService migrationService)
    {
        Console.WriteLine("MigrationOps");
        Console.WriteLine("  1) Dry-run (report only)");
        Console.WriteLine("  2) Dry-run + verify (executes pending scripts, then rolls back)");
        Console.WriteLine("  3) Apply");

        var action = PromptChoice("Select action [1]: ", max: 3, defaultChoice: 1);
        if (action == null)
        {
            return 1;
        }

        var databases = migrationService.GetDatabaseNames();
        Console.WriteLine("Target database: 1) All  " + string.Join("  ", databases.Select((name, i) => $"{i + 2}) {name}")));

        var target = PromptChoice("Select target [1]: ", max: databases.Count + 1, defaultChoice: 1);
        if (target == null)
        {
            return 1;
        }

        var database = target == 1 ? null : databases[target.Value - 2];

        return action switch
        {
            1 => RunDryRun(migrationService, database, verify: false),
            2 => RunDryRun(migrationService, database, verify: true),
            _ => RunApply(migrationService, database),
        };
    }

    // Empty input = default; invalid input re-prompts once, then gives up (null).
    private static int? PromptChoice(string prompt, int max, int defaultChoice)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Console.Write(prompt);
            var input = Console.ReadLine();

            if (input == null)
            {
                return null;
            }

            input = input.Trim();

            if (input.Length == 0)
            {
                return defaultChoice;
            }

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= max)
            {
                return choice;
            }

            Console.WriteLine($"Please enter a number between 1 and {max}.");
        }

        return null;
    }

    private static int RunApply(MigrationService migrationService, string? database)
    {
        // Database objects (functions, views, stored procedures, triggers) are applied before
        // migrations so that migration scripts can rely on the latest object definitions.
        // Object scripts that fail because they depend on schema a pending migration creates
        // are deferred and retried after migrations; a retry failure halts the run.
        var deferred = migrationService.ApplyDatabaseObjectScripts(migrationService.GetScriptDirectory(), database);
        migrationService.ApplyMigrations(migrationService.GetMigrationDirectory(), database);
        migrationService.RetryDeferredScripts(deferred, database);
        return 0;
    }

    private static int RunDryRun(MigrationService migrationService, string? database, bool verify)
    {
        var targets = database != null
            ? new List<string> { database }
            : migrationService.GetDatabaseNames();

        var plan = migrationService.BuildDryRunPlan(
            migrationService.GetScriptDirectory(),
            migrationService.GetMigrationDirectory(),
            targets);

        if (verify)
        {
            migrationService.VerifyPlan(plan);
        }

        return DryRunReportRenderer.Render(plan, verify) ? 0 : 1;
    }
}
