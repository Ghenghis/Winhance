"""
Documentation Automation System

Agents that automatically:
- Generate documentation from code
- Create diagrams (Mermaid, PlantUML)
- Detect and document issues/bugs
- Track updates and changes
- Maintain docs in sync with code
"""

from __future__ import annotations

import ast
import inspect
import re
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Optional, Dict, Any, List
import json

from nexus_ai.core.logging_config import get_logger
from nexus_ai.core.ai_providers import get_ai_manager, AIMessage, ProviderType

logger = get_logger("doc_automation")


class DocType(Enum):
    """Types of documentation."""
    README = "readme"
    API_REFERENCE = "api_reference"
    ARCHITECTURE = "architecture"
    CHANGELOG = "changelog"
    ISSUE_REPORT = "issue_report"
    BUG_REPORT = "bug_report"
    UPGRADE_GUIDE = "upgrade_guide"
    DIAGRAM = "diagram"


class DiagramType(Enum):
    """Types of diagrams."""
    FLOWCHART = "flowchart"
    SEQUENCE = "sequence"
    CLASS_DIAGRAM = "class"
    STATE_DIAGRAM = "state"
    ER_DIAGRAM = "er"
    COMPONENT = "component"
    ARCHITECTURE = "architecture"


@dataclass
class CodeElement:
    """Represents a code element (class, function, module)."""
    name: str
    type: str  # "class", "function", "module", "method"
    file_path: str
    line_number: int
    docstring: Optional[str] = None
    signature: Optional[str] = None
    dependencies: List[str] = field(default_factory=list)
    children: List["CodeElement"] = field(default_factory=list)


@dataclass
class IssueEntry:
    """Represents a detected issue."""
    id: str
    type: str  # "bug", "warning", "improvement", "missing"
    severity: str  # "critical", "high", "medium", "low"
    file_path: str
    line_number: Optional[int]
    description: str
    suggestion: Optional[str] = None
    detected_at: datetime = field(default_factory=datetime.now)


@dataclass
class DocSection:
    """A section of documentation."""
    title: str
    content: str
    order: int = 0
    auto_generated: bool = True
    last_updated: datetime = field(default_factory=datetime.now)


class CodeAnalyzer:
    """Analyzes code structure for documentation."""

    def __init__(self):
        self.elements: Dict[str, CodeElement] = {}
        self.issues: List[IssueEntry] = []

    def analyze_python_file(self, file_path: Path) -> List[CodeElement]:
        """Analyze a Python file and extract code elements."""
        elements = []

        try:
            source = file_path.read_text(encoding="utf-8")
            tree = ast.parse(source, filename=str(file_path))

            for node in ast.walk(tree):
                if isinstance(node, ast.ClassDef):
                    element = CodeElement(
                        name=node.name,
                        type="class",
                        file_path=str(file_path),
                        line_number=node.lineno,
                        docstring=ast.get_docstring(node),
                    )

                    # Analyze methods
                    for item in node.body:
                        if isinstance(item, ast.FunctionDef):
                            method = CodeElement(
                                name=item.name,
                                type="method",
                                file_path=str(file_path),
                                line_number=item.lineno,
                                docstring=ast.get_docstring(item),
                                signature=self._get_function_signature(item),
                            )
                            element.children.append(method)

                    elements.append(element)
                    self.elements[f"{file_path}:{node.name}"] = element

                elif isinstance(node, ast.FunctionDef) and node.col_offset == 0:
                    # Top-level function
                    element = CodeElement(
                        name=node.name,
                        type="function",
                        file_path=str(file_path),
                        line_number=node.lineno,
                        docstring=ast.get_docstring(node),
                        signature=self._get_function_signature(node),
                    )
                    elements.append(element)
                    self.elements[f"{file_path}:{node.name}"] = element

            # Check for issues
            self._check_for_issues(file_path, tree, source)

        except Exception as e:
            logger.error(f"Failed to analyze {file_path}: {e}")
            self.issues.append(IssueEntry(
                id=f"parse_error_{file_path.name}",
                type="bug",
                severity="high",
                file_path=str(file_path),
                line_number=None,
                description=f"Failed to parse file: {e}",
            ))

        return elements

    def _get_function_signature(self, node: ast.FunctionDef) -> str:
        """Extract function signature."""
        args = []
        for arg in node.args.args:
            arg_str = arg.arg
            if arg.annotation:
                arg_str += f": {ast.unparse(arg.annotation)}"
            args.append(arg_str)

        returns = ""
        if node.returns:
            returns = f" -> {ast.unparse(node.returns)}"

        return f"def {node.name}({', '.join(args)}){returns}"

    def _check_for_issues(self, file_path: Path, tree: ast.AST, source: str) -> None:
        """Check for common issues in code."""
        lines = source.split("\n")

        for node in ast.walk(tree):
            # Missing docstrings
            if isinstance(node, (ast.ClassDef, ast.FunctionDef)):
                if not ast.get_docstring(node):
                    self.issues.append(IssueEntry(
                        id=f"missing_doc_{file_path.name}_{node.name}",
                        type="warning",
                        severity="medium",
                        file_path=str(file_path),
                        line_number=node.lineno,
                        description=f"Missing docstring for {node.__class__.__name__} '{node.name}'",
                        suggestion="Add a docstring describing the purpose and usage",
                    ))

            # TODO comments
            if isinstance(node, ast.Expr) and isinstance(node.value, ast.Constant):
                if isinstance(node.value.value, str) and "TODO" in node.value.value:
                    self.issues.append(IssueEntry(
                        id=f"todo_{file_path.name}_{node.lineno}",
                        type="improvement",
                        severity="low",
                        file_path=str(file_path),
                        line_number=node.lineno,
                        description=node.value.value,
                    ))

        # Check for TODO/FIXME in comments
        for i, line in enumerate(lines, 1):
            if "#" in line:
                comment = line[line.index("#"):]
                if "TODO" in comment or "FIXME" in comment:
                    self.issues.append(IssueEntry(
                        id=f"comment_{file_path.name}_{i}",
                        type="improvement" if "TODO" in comment else "bug",
                        severity="low" if "TODO" in comment else "medium",
                        file_path=str(file_path),
                        line_number=i,
                        description=comment.strip("# "),
                    ))

    def analyze_rust_file(self, file_path: Path) -> List[CodeElement]:
        """Analyze a Rust file (basic parsing)."""
        elements = []

        try:
            source = file_path.read_text(encoding="utf-8")

            # Basic regex patterns for Rust
            # Functions
            for match in re.finditer(r'(pub\s+)?(async\s+)?fn\s+(\w+)\s*\([^)]*\)', source):
                line_num = source[:match.start()].count('\n') + 1
                elements.append(CodeElement(
                    name=match.group(3),
                    type="function",
                    file_path=str(file_path),
                    line_number=line_num,
                    signature=match.group(0),
                ))

            # Structs
            for match in re.finditer(r'(pub\s+)?struct\s+(\w+)', source):
                line_num = source[:match.start()].count('\n') + 1
                elements.append(CodeElement(
                    name=match.group(2),
                    type="struct",
                    file_path=str(file_path),
                    line_number=line_num,
                ))

            # Impl blocks
            for match in re.finditer(r'impl\s+(\w+)', source):
                line_num = source[:match.start()].count('\n') + 1
                elements.append(CodeElement(
                    name=match.group(1),
                    type="impl",
                    file_path=str(file_path),
                    line_number=line_num,
                ))

        except Exception as e:
            logger.error(f"Failed to analyze Rust file {file_path}: {e}")

        return elements

    def analyze_csharp_file(self, file_path: Path) -> List[CodeElement]:
        """Analyze a C# file (basic parsing)."""
        elements = []

        try:
            source = file_path.read_text(encoding="utf-8")

            # Classes
            for match in re.finditer(r'(public|internal|private)?\s*(class|interface)\s+(\w+)', source):
                line_num = source[:match.start()].count('\n') + 1
                elements.append(CodeElement(
                    name=match.group(3),
                    type=match.group(2),
                    file_path=str(file_path),
                    line_number=line_num,
                ))

            # Methods
            for match in re.finditer(r'(public|private|protected|internal)?\s*(async\s+)?([\w<>]+)\s+(\w+)\s*\([^)]*\)', source):
                line_num = source[:match.start()].count('\n') + 1
                elements.append(CodeElement(
                    name=match.group(4),
                    type="method",
                    file_path=str(file_path),
                    line_number=line_num,
                    signature=match.group(0),
                ))

        except Exception as e:
            logger.error(f"Failed to analyze C# file {file_path}: {e}")

        return elements


class DiagramGenerator:
    """Generates diagrams from code analysis."""

    def __init__(self, ai_manager=None):
        self.ai_manager = ai_manager or get_ai_manager()

    def generate_mermaid_flowchart(self, elements: List[CodeElement], title: str = "Code Flow") -> str:
        """Generate a Mermaid flowchart from code elements."""
        lines = [f"```mermaid", f"flowchart TD", f"    subgraph {title}"]

        for elem in elements:
            node_id = elem.name.replace(" ", "_")
            if elem.type == "class":
                lines.append(f"    {node_id}[{elem.name}]")
                for child in elem.children:
                    child_id = f"{node_id}_{child.name}"
                    lines.append(f"    {node_id} --> {child_id}(({child.name}))")
            elif elem.type == "function":
                lines.append(f"    {node_id}({elem.name})")

        lines.append("    end")
        lines.append("```")
        return "\n".join(lines)

    def generate_mermaid_class_diagram(self, elements: List[CodeElement]) -> str:
        """Generate a Mermaid class diagram."""
        lines = ["```mermaid", "classDiagram"]

        for elem in elements:
            if elem.type == "class":
                lines.append(f"    class {elem.name} {{")
                for child in elem.children:
                    if child.type == "method":
                        lines.append(f"        +{child.name}()")
                lines.append("    }")

        lines.append("```")
        return "\n".join(lines)

    def generate_architecture_diagram(self, project_path: Path) -> str:
        """Generate architecture diagram from project structure."""
        lines = [
            "```mermaid",
            "flowchart TB",
            "    subgraph Presentation[Presentation Layer]",
            "        WPF[Winhance.WPF]",
            "    end",
            "",
            "    subgraph Services[Service Layer]",
            "        Infra[Winhance.Infrastructure]",
            "    end",
            "",
            "    subgraph Domain[Domain Layer]",
            "        Core[Winhance.Core]",
            "    end",
            "",
            "    subgraph Native[Native Layer]",
            "        Rust[nexus_core - Rust]",
            "        Python[nexus_ai - Python]",
            "    end",
            "",
            "    WPF --> Infra",
            "    Infra --> Core",
            "    Infra --> Rust",
            "    Infra --> Python",
            "```",
        ]
        return "\n".join(lines)

    async def generate_ai_diagram(
        self,
        description: str,
        diagram_type: DiagramType = DiagramType.FLOWCHART,
    ) -> str:
        """Use AI to generate a diagram from description."""
        prompt = f"""Generate a Mermaid {diagram_type.value} diagram for:

{description}

Output only the Mermaid code block, no explanation."""

        try:
            messages = [
                AIMessage(role="system", content="You are a diagram expert. Generate clean Mermaid diagrams."),
                AIMessage(role="user", content=prompt),
            ]
            response = await self.ai_manager.chat(messages)
            return response.content
        except Exception as e:
            logger.error(f"AI diagram generation failed: {e}")
            return f"```mermaid\nflowchart TD\n    A[Error generating diagram]\n```"


class DocGenerator:
    """Generates documentation from code analysis."""

    def __init__(self, ai_manager=None):
        self.ai_manager = ai_manager or get_ai_manager()
        self.analyzer = CodeAnalyzer()
        self.diagram_gen = DiagramGenerator(ai_manager)

    def generate_api_reference(self, elements: List[CodeElement]) -> str:
        """Generate API reference documentation."""
        sections = ["# API Reference\n"]

        # Group by type
        classes = [e for e in elements if e.type == "class"]
        functions = [e for e in elements if e.type == "function"]

        if classes:
            sections.append("## Classes\n")
            for cls in classes:
                sections.append(f"### `{cls.name}`\n")
                if cls.docstring:
                    sections.append(f"{cls.docstring}\n")
                sections.append(f"**File:** `{cls.file_path}:{cls.line_number}`\n")

                if cls.children:
                    sections.append("\n#### Methods\n")
                    for method in cls.children:
                        sections.append(f"- `{method.signature or method.name}`")
                        if method.docstring:
                            sections.append(f"  - {method.docstring.split(chr(10))[0]}")
                        sections.append("")

        if functions:
            sections.append("\n## Functions\n")
            for func in functions:
                sections.append(f"### `{func.signature or func.name}`\n")
                if func.docstring:
                    sections.append(f"{func.docstring}\n")
                sections.append(f"**File:** `{func.file_path}:{func.line_number}`\n")

        return "\n".join(sections)

    def generate_issue_report(self, issues: List[IssueEntry]) -> str:
        """Generate an issue/bug report from detected issues."""
        sections = [
            "# Issue Report\n",
            f"**Generated:** {datetime.now().isoformat()}\n",
            f"**Total Issues:** {len(issues)}\n",
        ]

        # Group by severity
        by_severity = {}
        for issue in issues:
            by_severity.setdefault(issue.severity, []).append(issue)

        for severity in ["critical", "high", "medium", "low"]:
            if severity in by_severity:
                sections.append(f"\n## {severity.upper()} Severity ({len(by_severity[severity])})\n")
                for issue in by_severity[severity]:
                    sections.append(f"### {issue.type.upper()}: {issue.description[:50]}...\n")
                    sections.append(f"- **File:** `{issue.file_path}`")
                    if issue.line_number:
                        sections.append(f"- **Line:** {issue.line_number}")
                    sections.append(f"- **Description:** {issue.description}")
                    if issue.suggestion:
                        sections.append(f"- **Suggestion:** {issue.suggestion}")
                    sections.append("")

        return "\n".join(sections)

    def generate_changelog_entry(self, changes: List[Dict[str, Any]]) -> str:
        """Generate a changelog entry."""
        sections = [f"## [{datetime.now().strftime('%Y-%m-%d')}]\n"]

        added = [c for c in changes if c.get("type") == "added"]
        changed = [c for c in changes if c.get("type") == "changed"]
        fixed = [c for c in changes if c.get("type") == "fixed"]
        removed = [c for c in changes if c.get("type") == "removed"]

        if added:
            sections.append("### Added")
            for item in added:
                sections.append(f"- {item['description']}")

        if changed:
            sections.append("\n### Changed")
            for item in changed:
                sections.append(f"- {item['description']}")

        if fixed:
            sections.append("\n### Fixed")
            for item in fixed:
                sections.append(f"- {item['description']}")

        if removed:
            sections.append("\n### Removed")
            for item in removed:
                sections.append(f"- {item['description']}")

        return "\n".join(sections)

    async def generate_ai_documentation(
        self,
        code_snippet: str,
        doc_type: DocType,
    ) -> str:
        """Use AI to generate documentation for code."""
        prompts = {
            DocType.README: "Generate a README.md section for this code, including usage examples.",
            DocType.API_REFERENCE: "Generate API reference documentation for this code.",
            DocType.ARCHITECTURE: "Describe the architecture and design patterns in this code.",
        }

        prompt = prompts.get(doc_type, "Generate documentation for this code.")

        try:
            messages = [
                AIMessage(role="system", content="You are a technical writer. Generate clear, comprehensive documentation."),
                AIMessage(role="user", content=f"{prompt}\n\n```\n{code_snippet}\n```"),
            ]
            response = await self.ai_manager.chat(messages)
            return response.content
        except Exception as e:
            logger.error(f"AI documentation generation failed: {e}")
            return f"*Documentation generation failed: {e}*"


class DocumentationAgent:
    """
    Agent that continuously monitors and updates documentation.

    Capabilities:
    - Scans codebase for changes
    - Generates/updates documentation
    - Creates diagrams
    - Reports issues
    - Maintains doc-code sync
    """

    def __init__(self, project_path: Path, docs_path: Optional[Path] = None):
        self.project_path = project_path
        self.docs_path = docs_path or project_path / "docs"
        self.generator = DocGenerator()
        self.analyzer = CodeAnalyzer()
        self._last_scan: Optional[datetime] = None

    async def full_scan(self) -> Dict[str, Any]:
        """
        Perform a full scan of the codebase.

        Returns summary of findings.
        """
        results = {
            "files_scanned": 0,
            "elements_found": 0,
            "issues_found": 0,
            "docs_generated": [],
        }

        # Scan Python files
        for py_file in self.project_path.rglob("*.py"):
            if "__pycache__" in str(py_file):
                continue
            elements = self.analyzer.analyze_python_file(py_file)
            results["files_scanned"] += 1
            results["elements_found"] += len(elements)

        # Scan Rust files
        for rs_file in self.project_path.rglob("*.rs"):
            if "target" in str(rs_file):
                continue
            elements = self.analyzer.analyze_rust_file(rs_file)
            results["files_scanned"] += 1
            results["elements_found"] += len(elements)

        # Scan C# files
        for cs_file in self.project_path.rglob("*.cs"):
            if "bin" in str(cs_file) or "obj" in str(cs_file):
                continue
            elements = self.analyzer.analyze_csharp_file(cs_file)
            results["files_scanned"] += 1
            results["elements_found"] += len(elements)

        results["issues_found"] = len(self.analyzer.issues)
        self._last_scan = datetime.now()

        return results

    async def generate_all_docs(self) -> List[Path]:
        """Generate all documentation files."""
        generated = []

        self.docs_path.mkdir(parents=True, exist_ok=True)

        # API Reference
        api_doc = self.generator.generate_api_reference(list(self.analyzer.elements.values()))
        api_path = self.docs_path / "API_REFERENCE_AUTO.md"
        api_path.write_text(api_doc)
        generated.append(api_path)

        # Issue Report
        if self.analyzer.issues:
            issue_doc = self.generator.generate_issue_report(self.analyzer.issues)
            issue_path = self.docs_path / "ISSUE_REPORT_AUTO.md"
            issue_path.write_text(issue_doc)
            generated.append(issue_path)

        # Architecture Diagram
        arch_diagram = self.generator.diagram_gen.generate_architecture_diagram(self.project_path)
        diag_path = self.docs_path / "ARCHITECTURE_DIAGRAM.md"
        diag_path.write_text(f"# Architecture Diagram\n\n{arch_diagram}")
        generated.append(diag_path)

        # Class Diagrams
        classes = [e for e in self.analyzer.elements.values() if e.type == "class"]
        if classes:
            class_diag = self.generator.diagram_gen.generate_mermaid_class_diagram(classes)
            class_path = self.docs_path / "CLASS_DIAGRAM.md"
            class_path.write_text(f"# Class Diagram\n\n{class_diag}")
            generated.append(class_path)

        logger.info(f"Generated {len(generated)} documentation files")
        return generated

    def get_status(self) -> Dict[str, Any]:
        """Get agent status."""
        return {
            "project_path": str(self.project_path),
            "docs_path": str(self.docs_path),
            "last_scan": self._last_scan.isoformat() if self._last_scan else None,
            "elements_tracked": len(self.analyzer.elements),
            "issues_tracked": len(self.analyzer.issues),
        }


# Global documentation agent
_doc_agent: Optional[DocumentationAgent] = None


def get_doc_agent(project_path: Optional[Path] = None) -> DocumentationAgent:
    """Get the global documentation agent."""
    global _doc_agent
    if _doc_agent is None:
        project_path = project_path or Path("D:/Winhance-FS-Repo")
        _doc_agent = DocumentationAgent(project_path)
    return _doc_agent
