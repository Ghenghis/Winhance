"""
NexusFS CLI Main Entry Point

Ultra-fast AI-powered file organization for Windows.
Faster than Everything Search with AI-powered features.
"""

import sys
from pathlib import Path

import typer
from rich.console import Console
from rich.panel import Panel
from rich.progress import BarColumn, Progress, SpinnerColumn, TextColumn
from rich.table import Table

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

app = typer.Typer(
    name="nexus",
    help="NexusFS - Ultra-Fast AI-Powered File Organization for Windows",
    add_completion=True,
    rich_markup_mode="rich",
)
console = Console()

# Sub-command groups
index_app = typer.Typer(help="Indexing commands - Build and manage file indices")
search_app = typer.Typer(help="Search commands - Find files fast")
organize_app = typer.Typer(help="Organization commands - AI-powered file organization")
rollback_app = typer.Typer(help="Rollback commands - Undo file operations")
space_app = typer.Typer(help="Space commands - Analyze and manage disk space")
model_app = typer.Typer(help="Model commands - Manage AI model files")
ai_app = typer.Typer(help="AI commands - AI assistant integration")
mcp_app = typer.Typer(help="MCP commands - Model Context Protocol server")

app.add_typer(index_app, name="index")
app.add_typer(search_app, name="search")
app.add_typer(organize_app, name="organize")
app.add_typer(rollback_app, name="rollback")
app.add_typer(space_app, name="space")
app.add_typer(model_app, name="model")
app.add_typer(ai_app, name="ai")
app.add_typer(mcp_app, name="mcp")


# =============================================================================
# Main Commands
# =============================================================================

@app.command()
def init(
    global_: bool = typer.Option(False, "--global", "-g", help="Initialize globally"),
    data_dir: str | None = typer.Option(None, "--data-dir", help="Custom data directory"),
):
    """Initialize NexusFS configuration and data directories."""
    from nexus_ai.config import NexusConfig, set_config

    data_path = Path(data_dir) if data_dir else Path("D:/NexusFS/data")

    # Create directories
    dirs_to_create = [
        data_path / "indices",
        data_path / "vectors",
        data_path / "transactions",
        data_path / "backups",
        data_path / "cache",
        data_path / "logs",
    ]

    for d in dirs_to_create:
        d.mkdir(parents=True, exist_ok=True)

    # Save config
    config = NexusConfig(data_dir=data_path)
    config.save()
    set_config(config)

    console.print(f"[green]NexusFS initialized at {data_path}[/green]")
    console.print("\nDirectories created:")
    for d in dirs_to_create:
        console.print(f"  [blue]{d}[/blue]")


@app.command()
def status():
    """Show NexusFS status and statistics."""
    from nexus_ai.config import get_config

    config = get_config()

    console.print(Panel.fit(
        "[bold cyan]NexusFS Status[/bold cyan]",
        border_style="cyan"
    ))

    table = Table(show_header=False, box=None)
    table.add_column("Key", style="yellow")
    table.add_column("Value", style="white")

    table.add_row("Data Directory", str(config.data_dir))
    table.add_row("Index Threads", str(config.index.threads))
    table.add_row("Drives", ", ".join(config.index.drives))

    # Check index stats
    index_path = config.search.tantivy_index_path
    if index_path.exists():
        table.add_row("Index Status", "[green]Ready[/green]")
    else:
        table.add_row("Index Status", "[yellow]Not built[/yellow]")

    console.print(table)


# =============================================================================
# Index Commands
# =============================================================================

@index_app.command("build")
def index_build(
    path: str | None = typer.Argument(None, help="Path to index (default: all drives)"),
    deep: bool = typer.Option(False, "--deep", "-d", help="Include content embeddings"),
    watch: bool = typer.Option(False, "--watch", "-w", help="Start real-time monitoring"),
    threads: int = typer.Option(0, "--threads", "-t", help="Number of threads (0=auto)"),
):
    """Build or rebuild the file index."""
    from nexus_ai.config import get_config
    from nexus_ai.indexer.hyper_indexer import HyperIndexer

    config = get_config()

    if threads == 0:
        threads = config.index.threads * 2  # I/O bound, use more threads

    indexer = HyperIndexer(
        threads=threads,
        deep_index=deep,
    )

    with Progress(
        SpinnerColumn(),
        TextColumn("[progress.description]{task.description}"),
        BarColumn(),
        TextColumn("{task.completed:,} files"),
        console=console,
    ) as progress:
        task = progress.add_task("Indexing...", total=None)

        def update_progress(current_path: str, count: int):
            progress.update(task, completed=count, description=f"Indexing: {current_path[:50]}...")

        if path:
            stats = indexer.index_path(Path(path), progress_callback=update_progress)
        else:
            stats = indexer.index_all_drives(config.index.drives, progress_callback=update_progress)

    console.print("\n[green]Indexing complete![/green]")
    console.print(f"  Files: {stats['file_count']:,}")
    console.print(f"  Directories: {stats['dir_count']:,}")
    console.print(f"  Time: {stats['time_ms']}ms")
    console.print(f"  Speed: {stats['file_count'] / (stats['time_ms'] / 1000):.0f} files/sec")


@index_app.command("status")
def index_status():
    """Show index status and statistics."""
    from nexus_ai.config import get_config

    config = get_config()
    index_path = config.search.tantivy_index_path

    if not index_path.exists():
        console.print("[yellow]Index not built. Run 'nexus index build' first.[/yellow]")
        return

    # Get index stats
    console.print(f"[green]Index exists at {index_path}[/green]")


# =============================================================================
# Search Commands
# =============================================================================

@search_app.callback(invoke_without_command=True)
def search_main(
    ctx: typer.Context,
    query: str | None = typer.Argument(None, help="Search query"),
    type_: str = typer.Option("semantic", "--type", "-t", help="Search type: semantic, glob, regex, exact"),
    limit: int = typer.Option(20, "--limit", "-l", help="Maximum results"),
    extensions: str | None = typer.Option(None, "--ext", "-e", help="Filter by extensions (comma-separated)"),
    min_size: str | None = typer.Option(None, "--min-size", help="Minimum file size (e.g., 100MB, 1GB)"),
    max_size: str | None = typer.Option(None, "--max-size", help="Maximum file size"),
    drives: str | None = typer.Option(None, "--drives", "-d", help="Filter by drives (comma-separated)"),
    dirs_only: bool = typer.Option(False, "--dirs", help="Only show directories"),
    files_only: bool = typer.Option(False, "--files", help="Only show files"),
):
    """Search for files using various methods."""
    if ctx.invoked_subcommand is not None:
        return

    if not query:
        console.print("[yellow]Please provide a search query[/yellow]")
        raise typer.Exit(1)

    # Parse options
    ext_list = [e.strip() for e in extensions.split(",")] if extensions else None
    drive_list = [d.strip().upper() for d in drives.split(",")] if drives else None

    console.print(f"Searching for: [cyan]{query}[/cyan]")

    # TODO: Implement actual search
    # For now, show placeholder
    table = Table(title=f"Search Results: {query}")
    table.add_column("Score", style="cyan", width=8)
    table.add_column("Size", style="yellow", width=12)
    table.add_column("Path", style="white")

    # Placeholder results
    console.print("[yellow]Search index not built yet. Run 'nexus index build' first.[/yellow]")


@search_app.command("similar")
def search_similar(
    file_path: str = typer.Argument(..., help="Path to file to find similar files"),
    limit: int = typer.Option(10, "--limit", "-l", help="Maximum results"),
):
    """Find files similar to a given file."""
    console.print(f"Finding files similar to: [cyan]{file_path}[/cyan]")
    console.print("[yellow]Semantic search requires index with embeddings. Run 'nexus index build --deep'[/yellow]")


@search_app.command("duplicates")
def search_duplicates(
    path: str | None = typer.Argument(None, help="Path to scan (default: all indexed)"),
    min_size: str = typer.Option("1MB", "--min-size", help="Minimum file size for duplicates"),
):
    """Find duplicate files."""
    console.print(f"Scanning for duplicates (min size: {min_size})...")
    console.print("[yellow]Not implemented yet[/yellow]")


# =============================================================================
# Space Commands
# =============================================================================

@space_app.command("analyze")
def space_analyze(
    path: str = typer.Argument("C:\\Users\\Admin", help="Path to analyze"),
    large_gb: float = typer.Option(1.0, "--large", help="Large file threshold (GB)"),
    huge_gb: float = typer.Option(10.0, "--huge", help="Huge file threshold (GB)"),
    hashes: bool = typer.Option(False, "--hashes", help="Compute hashes for duplicate detection"),
):
    """Analyze disk space usage with hyper-threaded scanning."""
    from nexus_ai.tools.space_analyzer import SpaceAnalyzer

    console.print(f"[cyan]Analyzing {path}...[/cyan]")

    analyzer = SpaceAnalyzer(
        large_threshold_gb=large_gb,
        huge_threshold_gb=huge_gb,
        compute_hashes=hashes,
    )

    with Progress(
        SpinnerColumn(),
        TextColumn("[progress.description]{task.description}"),
        console=console,
    ) as progress:
        task = progress.add_task("Scanning...", total=None)

        def update(p: str, count: int):
            progress.update(task, description=f"Scanned {count:,} files...")

        analysis = analyzer.analyze_path(path)

    analyzer.print_report(analysis)


@space_app.command("large")
def space_large(
    path: str = typer.Argument("C:\\", help="Path to scan"),
    min_gb: float = typer.Option(1.0, "--min", help="Minimum size in GB"),
    limit: int = typer.Option(50, "--limit", "-l", help="Maximum results"),
):
    """Find large files quickly."""
    from nexus_ai.tools.space_analyzer import SpaceAnalyzer

    console.print(f"[cyan]Finding files larger than {min_gb} GB in {path}...[/cyan]")

    analyzer = SpaceAnalyzer()
    large_files = analyzer.find_large_files(path, min_size_gb=min_gb, limit=limit)

    table = Table(title=f"Large Files (>{min_gb} GB)")
    table.add_column("Size", style="red", width=12)
    table.add_column("Modified", style="yellow", width=12)
    table.add_column("Path", style="white")

    for f in large_files:
        table.add_row(
            analyzer.format_size(f.size),
            f.modified.strftime("%Y-%m-%d"),
            f.path,
        )

    console.print(table)
    total = sum(f.size for f in large_files)
    console.print(f"\n[cyan]Total: {analyzer.format_size(total)} in {len(large_files)} files[/cyan]")


# =============================================================================
# Model Commands
# =============================================================================

@model_app.command("scan")
def model_scan(
    app: str | None = typer.Option(None, "--app", "-a", help="Specific app (lmstudio, ollama, huggingface)"),
):
    """Scan for AI model files."""
    from nexus_ai.tools.model_relocator import ModelRelocator

    relocator = ModelRelocator()
    relocator.print_summary()


@model_app.command("relocate")
def model_relocate(
    dest: str = typer.Option("G", "--dest", "-d", help="Destination drive letter"),
    min_gb: float = typer.Option(1.0, "--min", help="Minimum model size to relocate"),
    dry_run: bool = typer.Option(True, "--dry-run/--execute", help="Dry run vs actual execution"),
):
    """Relocate models to another drive with symlinks."""
    from nexus_ai.tools.model_relocator import ModelRelocator

    relocator = ModelRelocator()
    plan = relocator.create_relocation_plan(
        dest_drive=dest,
        min_size_gb=min_gb,
    )

    console.print("\n[cyan]Relocation Plan:[/cyan]")
    console.print(f"  Models: {len(plan.models)}")
    console.print(f"  Total Size: {plan.total_size / 1024**3:.2f} GB")
    console.print(f"  Destination: {plan.dest_dir}")
    console.print(f"  Create Symlinks: {plan.create_symlink}")

    if dry_run:
        console.print("\n[yellow]DRY RUN - no files will be moved[/yellow]")
    else:
        console.print("\n[red]EXECUTING - files will be moved![/red]")

    results = relocator.execute_relocation(plan, dry_run=dry_run)

    success = sum(1 for r in results if r.success)
    console.print(f"\n[green]Complete: {success}/{len(results)} models processed[/green]")


@model_app.command("suggest")
def model_suggest(
    free_gb: float = typer.Option(100.0, "--free", "-f", help="Target GB to free up"),
    dest: str = typer.Option("G", "--dest", "-d", help="Destination drive"),
):
    """Suggest models to relocate to free up space."""
    from nexus_ai.tools.model_relocator import ModelRelocator

    relocator = ModelRelocator()
    relocator.suggest_relocations(target_free_gb=free_gb, dest_drive=dest)


# =============================================================================
# Rollback Commands
# =============================================================================

@rollback_app.command("list")
def rollback_list(
    hours: int | None = typer.Option(None, "--hours", "-h", help="Show transactions from last N hours"),
    limit: int = typer.Option(20, "--limit", "-l", help="Maximum transactions to show"),
):
    """List recent file transactions."""
    from nexus_ai.config import get_config
    from nexus_ai.organization.transaction_manager import TransactionManager

    config = get_config()
    tm = TransactionManager(
        log_path=config.transaction.log_path,
        backup_dir=config.transaction.backup_dir,
    )

    transactions = tm.list_transactions(limit=limit)

    if not transactions:
        console.print("[yellow]No transactions found[/yellow]")
        return

    table = Table(title="Recent Transactions")
    table.add_column("ID", style="cyan", width=8)
    table.add_column("Time", style="yellow", width=16)
    table.add_column("Op", style="magenta", width=6)
    table.add_column("Status", width=10)
    table.add_column("Source", style="white")

    for tx in transactions:
        status_style = {
            "completed": "[green]completed[/green]",
            "failed": "[red]failed[/red]",
            "rolled_back": "[blue]rolled_back[/blue]",
            "pending": "[yellow]pending[/yellow]",
        }.get(tx.status.value, tx.status.value)

        table.add_row(
            tx.id[:8],
            tx.timestamp.strftime("%Y-%m-%d %H:%M"),
            tx.operation.value,
            status_style,
            tx.source_path[-50:] if len(tx.source_path) > 50 else tx.source_path,
        )

    console.print(table)


@rollback_app.command("undo")
def rollback_undo(
    tx_id: str | None = typer.Argument(None, help="Transaction ID to rollback"),
    hours: int | None = typer.Option(None, "--hours", "-h", help="Rollback all in last N hours"),
):
    """Rollback file operations."""
    from nexus_ai.config import get_config
    from nexus_ai.organization.transaction_manager import TransactionManager

    config = get_config()
    tm = TransactionManager(
        log_path=config.transaction.log_path,
        backup_dir=config.transaction.backup_dir,
    )

    if tx_id:
        success = tm.rollback(tx_id)
        if success:
            console.print(f"[green]Successfully rolled back transaction {tx_id}[/green]")
        else:
            console.print(f"[red]Failed to rollback transaction {tx_id}[/red]")
    elif hours:
        rolled_back = tm.rollback_range(hours=hours)
        console.print(f"[green]Rolled back {len(rolled_back)} transactions[/green]")
    else:
        console.print("[yellow]Please specify a transaction ID or --hours[/yellow]")


@rollback_app.command("script")
def rollback_script(
    hours: int = typer.Option(1, "--hours", "-h", help="Generate script for last N hours"),
    format_: str = typer.Option("ps1", "--format", "-f", help="Script format: ps1 or bat"),
    output: str | None = typer.Option(None, "--output", "-o", help="Output file path"),
):
    """Generate a rollback script."""
    from nexus_ai.config import get_config
    from nexus_ai.organization.transaction_manager import TransactionManager

    config = get_config()
    tm = TransactionManager(
        log_path=config.transaction.log_path,
        backup_dir=config.transaction.backup_dir,
    )

    script = tm.generate_rollback_script(hours=hours, format=format_)

    if output:
        Path(output).write_text(script)
        console.print(f"[green]Script saved to {output}[/green]")
    else:
        console.print(script)


# =============================================================================
# MCP Commands
# =============================================================================

@mcp_app.command("start")
def mcp_start():
    """Start the MCP server for AI tool integration."""
    console.print("[cyan]Starting MCP server...[/cyan]")
    console.print("[yellow]Run 'python -m nexus_mcp' to start the server[/yellow]")


@mcp_app.command("status")
def mcp_status():
    """Check MCP server status."""
    console.print("[yellow]MCP server status check not implemented[/yellow]")


# =============================================================================
# Entry Point
# =============================================================================

def main():
    """Main entry point."""
    app()


if __name__ == "__main__":
    main()
