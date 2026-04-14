# Contributing to SslManager

First, thank you for considering contributing to SslManager! We welcome PRs for bug fixes, new features, and documentation improvements.

## Development Setup

1. You will need the **.NET 10 SDK** installed on your machine.
2. Fork the repository and clone it locally.
3. The solution contains three projects:
   * **`SslManager.Core`**: Handles all native cryptography and OS keychain integrations. No UI code goes here.
   * **`SslManager.Cli`**: A terminal shell utilizing `Cocona`.
   * **`SslManager.Tray`**: A background Avalonia UI application.

## Testing your changes

You can build and run both the CLI and Tray app locally via the `dotnet run` commands.
```bash
dotnet run --project SslManager.Cli -- init
dotnet run --project SslManager.Tray
```

## Pull Request Guidelines

1. **Keep it focused**: Do not bundle multiple unrelated features or rewrites into a single PR.
2. **Follow existing styling**: Match the code formatting and architectural patterns (e.g. MVVM for Avalonia, minimal APIs for Cocona).
3. **Draft PRs**: If you are working on a massive feature (e.g. implementing ACME HTTP-01 challenge support), please open a Draft PR early so we can discuss the architecture before you spend hours coding!

## Security

If you discover a cryptographic vulnerability regarding how Root CAs or leaf certificates are parsed and injected, please DO NOT open a public issue. Email the repository owner privately.
