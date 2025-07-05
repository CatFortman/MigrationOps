# MigrationTracker

**MigrationTracker** is an open-source project designed to combine the principles of SQL source control and migration management. 
Inspired by tools like Redgate's SQL source control and Entity Framework's migration feature, MigrationTracker helps you track and apply changes to your database schema efficiently and effectively.

## Features

- **SQL Source Control**: Organize your SQL scripts (stored procedures, views, functions, and triggers) in dedicated folders.
- **Automatic Checksum Calculation**: Inserts unique checksums into your SQL scripts to easily manage changes.
- **Migration Management**: Easily handle database migrations with a structured approach, using timestamped filenames to ensure proper execution order.
- **Git Integration**: Git hooks to automate checksum calculation and ensure valid script tags exist.

## Installation

### Prerequisites

- **Git**: Ensure Git is installed on your machine. You can download it from [Git for Windows](https://gitforwindows.org/).
- **Git Bash**: Git Bash should be installed, as the Git hooks are written in shell script format (`.sh`).

### Cloning the Repository

To get started, clone the project repository to your local machine:

```bash
git clone <repository-url>
```

### Setting Up Git Hooks
To ensure that our custom Git hooks are correctly set up on your local environment, please follow these steps:

1. Navigate to the root of the repository
   
2. Execute the GitHook setup script in PowerShell
```powershell
.\setup_hooks.ps1
```

3. Verify the Hook
Make a change in the repository, stage it, and attempt to commit. The pre-commit hook should automatically run and insert/update the checksum in your SQL files.

### Pre-commit Hook Compatibility

### Windows Environment
The pre-commit hook script uses PowerShell's `Get-FileHash` cmdlet to calculate SHA-256 checksums. This makes the script fully compatible with Windows environments, including GitHub Desktop and Git Bash.

### Other Environments (macOS/Linux)
If you're working in a macOS or Linux environment, you may need to modify the script to use a Unix-compatible command like `sha256sum`. Here’s a basic example:

```sh
#!/bin/sh
# Function to calculate SHA-256 checksum using sha256sum
calculate_checksum() {
    sha256sum "$1" | awk '{ print $1 }'
}

# Rest of the script...
```

## Usage

### Configuration Setup

**dbconfig.json** is used to configure the database connections and migration settings for MigrationTracker.

#### Example Structure:

```json
{
  "Databases": {
    "Db1": {
      "ConnectionString": "Server=myServerAddress;Database=db1;User Id=myUsername;Password=myPassword;"
    },
    "Db2": {
      "ConnectionString": "Server=myServerAddress;Database=db2;User Id=myUsername;Password=myPassword;"
    }
  },
  "MigrationSettings": {
    "MigrationDirectory": "Migrations",
    "ScriptDirectory": "Scripts",
    "DefaultDatabase": "Db1"
  }
}
```

### Organizing SQL Scripts
Place your SQL scripts into the appropriate folders:

* StoredProcedures/
* Views/
* Functions/
* Triggers/

Ensure that all scripts are written using the CREATE OR ALTER statement to simplify deployment.

### Organizing Migration Scripts
Scripts will be applied in the order determined by their filenames.

Migrations will be executed in order by datetime, then script number.

Example:
`
Migrations/20240805-001-CreateNewTestSchema.sql
Migrations/20240805-002-CreateEntityTable.sql
Migrations/20240806-001-CreateHappyCustomerTable.sql
Migrations/20240806-002-CreateCustomerTestTable.sql
`

### Handling Migrations
Add your migration scripts to the Migrations folder with a filename format that includes a timestamp and a brief description:

Example:
`
Migrations/20240805-001-CreateEntityTable.sql
`

### Running Migrations
When you deploy the project, the migration scripts will be applied to the databases in the ``Tags`` comment. Tags should be included as a comment at the top of each SQL script.
The githook runs validation logic to ensure the ``Tags`` comment is properly added to each sql file.

Example:
```SQL
-- Tags: db1, staging
CREATE OR ALTER PROCEDURE dbo.MyProcedure
AS
BEGIN
    -- Procedure logic here
END

```

## Contributing
I welcome contributions! Please fork the repository and submit a pull request for any enhancements or bug fixes.

## License
This project is licensed under the MIT License - see the LICENSE file for details.

## Contact
For any questions or suggestions, please open an issue or reach out to Cat Fortman.
