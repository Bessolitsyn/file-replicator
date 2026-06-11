# Testing

This project uses **xUnit** for unit and integration testing to ensure the reliability of the file replication process.

## Test Suite Overview

The test suite is located in the `FileReplicator/FileReplicatorTests` directory and covers the following areas:

### 1. Unit Tests
- **`FolderSyncTests`**: Validates the core logic of syncing individual files, including:
  - Successful first-time copies.
  - Skipping files that are already up-to-date.
  - Overwriting files when the source is newer.
  - Handling locked files.
  - Deleting files in the destination that no longer exist in the source.
- **`FileSynchronizerTests`**: Tests the `FileProcessor` class, focusing on:
  - Parallel processing of multiple files.
  - Mixed scenarios (simultaneous synchronization and deletion).
  - Error handling and population of the error queue.
- **`SettingsTests`**: Ensures that application settings are correctly loaded and parsed from JSON.
- **`FileCopyLogTest`**: Verifies the logging mechanism for file operations.

### 2. Integration Tests
- **`ReplicatorTests`**: Performs end-to-end tests of the `Replicator` class. It validates:
  - Recursive directory replication (nested folders).
  - Proper application of settings to the replication process.
- **`FolderSyncIntegrationTests`**: Validates the interaction between `FolderSync` and the file system across larger directory structures.

## How to Run Tests

You can run the tests using the .NET CLI:

```bash
dotnet test FileReplicator/FileReplicatorTests/FileReplicatorTests.csproj
```

Or use the Visual Studio Test Explorer for a graphical interface.
