# MigrationOps

SQL Server schema versioning tool. Migrations are plain .sql files executed in
filename order by the ConsoleApp. Solution has three projects:
`MigrationOps.ConsoleApp` (runner), `MigrationOps.Core` (framework), and
`MigrationOps.Dashboard` (read-only Razor Pages web UI over the same Core
logic).

## Build and run

Requires the .NET 8 SDK.

- Build (repo root): `dotnet build MigrationOps.sln`
- Run: `cd MigrationOps.ConsoleApp && dotnet run`

Always run from `MigrationOps.ConsoleApp/`. `Configurations/dbconfig.json` and
the `Migrations` folder are resolved relative to the working directory, so
`dotnet run --project` from the repo root silently loads no configuration.

- Dashboard: `cd MigrationOps.Dashboard && dotnet run` (listens on
  `http://localhost:5280`). Also working-directory-sensitive: its
  `appsettings.json` points at the ConsoleApp's `dbconfig.json` and
  `Migrations` folder via relative paths. Requires a pre-created dashboard
  database (`DashboardStore:ConnectionString`) for login accounts; the app
  creates its `__DashboardUsers` table but never the database itself. First
  account is bootstrapped at `/Register`, which closes once any user exists.

## Migration files

- Live in `MigrationOps.ConsoleApp/Migrations/`, named
  `yyyyMMdd-NNN-Description.sql` (e.g. `20240807-001-CreateUserTable.sql`).
  `NNN` is a zero-padded per-day sequence starting at 001; `Description` is
  PascalCase, no spaces.
- Every .sql file needs a `-- Tags:` comment. Tags are DATABASE TARGETS, not
  labels: each tag must match a key under `Databases` in
  `Configurations/dbconfig.json` (case-insensitive, e.g. `db1`, `db2`). The
  runner routes the script to each tagged database and throws if no tag
  matches a configured database.
- Scripts run as a single SqlCommand batch: no `GO` separators.
- Never edit a migration that has already been applied. Applied-state is
  matched on filename AND checksum, so editing the file changes its checksum
  and the runner re-executes it against the database. Fixes go in a new
  migration.
- Reusable objects (procs, views, functions, triggers) go under `Scripts/`
  using `CREATE OR ALTER`, not in `Migrations/`.
- To create a new migration, use the `/new-migration` skill.

## Pre-commit hook rewrites checksums

The pre-commit hook prepends or updates a `-- Checksum: <SHA256>` line as the
FIRST line of every staged .sql file (the `-- Tags:` line ends up second).
A file you just wrote WILL differ on disk after commit. This is expected.
Do not add checksum lines yourself, do not "fix" or revert them, and do not
treat the post-commit diff as corruption. The hook also rejects commits of
.sql files missing the `-- Tags:` comment.
