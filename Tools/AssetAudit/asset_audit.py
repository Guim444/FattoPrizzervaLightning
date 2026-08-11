#!/usr/bin/env python3
"""Auditoría estática y no destructiva para proyectos Unity.

Solo utiliza la biblioteca estándar de Python. Genera una carpeta nueva por
ejecución y nunca elimina informes anteriores ni archivos del proyecto.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
from collections import Counter, defaultdict, deque
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Dict, Iterable, Iterator, List, Optional, Sequence, Set, Tuple


REPORTS_DIRECTORY = "AssetAuditReports"

EXCLUDED_DIRECTORY_NAMES = {
    ".git",
    ".venv",
    ".vs",
    "__pycache__",
    "library",
    "temp",
    "logs",
    "obj",
    "build",
    "builds",
    "usersettings",
    "memorycaptures",
    "recordings",
    REPORTS_DIRECTORY.lower(),
}

UNITY_REFERENCE_EXTENSIONS = {
    ".anim",
    ".asset",
    ".controller",
    ".lighting",
    ".mat",
    ".overridecontroller",
    ".playable",
    ".prefab",
    ".rendertexture",
    ".shadergraph",
    ".shadersubgraph",
    ".spriteatlas",
    ".terrainlayer",
    ".unity",
    ".vfx",
}

UNUSED_CANDIDATE_EXTENSIONS = {
    ".abc",
    ".aif",
    ".aiff",
    ".anim",
    ".blend",
    ".controller",
    ".cubemap",
    ".exr",
    ".fbx",
    ".hdr",
    ".jpeg",
    ".jpg",
    ".mat",
    ".mov",
    ".mp3",
    ".mp4",
    ".obj",
    ".ogg",
    ".overridecontroller",
    ".playable",
    ".png",
    ".prefab",
    ".psd",
    ".rendertexture",
    ".shadergraph",
    ".spriteatlas",
    ".tga",
    ".tif",
    ".tiff",
    ".unity",
    ".vfx",
    ".wav",
    ".webm",
}

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg"}
TEXTURE_EXTENSIONS = {
    ".bmp",
    ".exr",
    ".gif",
    ".hdr",
    ".jpeg",
    ".jpg",
    ".png",
    ".psd",
    ".tga",
    ".tif",
    ".tiff",
}

GUID_RE = re.compile(r"\bguid:\s*([0-9a-fA-F]{32})\b")
META_GUID_RE = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
MAX_TEXTURE_RE = re.compile(r"\bmaxTextureSize:\s*(\d+)")


@dataclass
class FileRecord:
    path: str
    size_bytes: int
    extension: str
    area: str
    tracked: bool = False
    lfs: bool = False
    width: Optional[int] = None
    height: Optional[int] = None
    importer_max_sizes: str = ""
    readable: Optional[bool] = None
    mipmaps_disabled: Optional[bool] = None
    streaming_mipmaps: Optional[bool] = None
    crunch: Optional[bool] = None
    usage_state: str = "not_assessed"
    reference_count: int = 0

    @property
    def size_mb(self) -> float:
        return self.size_bytes / (1024 * 1024)


@dataclass
class DuplicateGroup:
    sha256: str
    size_bytes_each: int
    copies: int
    wasted_bytes: int
    paths: List[str]


@dataclass
class MetaIssue:
    issue: str
    path: str


def parse_args() -> argparse.Namespace:
    script_default_root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(
        description="Auditoría estática, repetible y no destructiva para un proyecto Unity."
    )
    parser.add_argument(
        "--project",
        type=Path,
        default=script_default_root,
        help="Raíz del proyecto Unity. Por defecto se infiere desde la ubicación del script.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help=f"Carpeta base para informes. Por defecto: <proyecto>/{REPORTS_DIRECTORY}",
    )
    parser.add_argument(
        "--top",
        type=int,
        default=100,
        help="Cantidad máxima de elementos mostrados en las tablas principales (por defecto: 100).",
    )
    parser.add_argument(
        "--large-mb",
        type=float,
        default=10.0,
        help="Umbral de archivo grande y control LFS en MB (por defecto: 10).",
    )
    parser.add_argument(
        "--duplicate-min-kb",
        type=int,
        default=64,
        help="Tamaño mínimo para calcular duplicados exactos (por defecto: 64 KB).",
    )
    parser.add_argument(
        "--skip-duplicates",
        action="store_true",
        help="Omite hashes de duplicados para una ejecución más rápida.",
    )
    return parser.parse_args()


def normalize_relative(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def make_output_directory(base: Path, timestamp: str) -> Path:
    base.mkdir(parents=True, exist_ok=True)
    preferred = base / f"asset_audit_{timestamp}"
    candidate = preferred
    suffix = 1
    while candidate.exists():
        candidate = base / f"asset_audit_{timestamp}_{suffix:02d}"
        suffix += 1
    candidate.mkdir(parents=False, exist_ok=False)
    return candidate


def walk_project_files(project_root: Path, output_base: Path) -> Iterator[Path]:
    root_resolved = project_root.resolve()
    output_resolved = output_base.resolve()

    for current, dirs, files in os.walk(root_resolved):
        current_path = Path(current)
        kept_dirs = []
        for name in dirs:
            child = (current_path / name).resolve()
            if name.lower() in EXCLUDED_DIRECTORY_NAMES:
                continue
            if child == output_resolved or output_resolved in child.parents:
                continue
            kept_dirs.append(name)
        dirs[:] = kept_dirs

        for name in files:
            path = current_path / name
            try:
                if path.is_file() and not path.is_symlink():
                    yield path
            except OSError:
                continue


def run_process(command: Sequence[str], cwd: Path) -> Tuple[bool, str]:
    try:
        completed = subprocess.run(
            list(command),
            cwd=str(cwd),
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
    except (FileNotFoundError, OSError):
        return False, ""
    return completed.returncode == 0, completed.stdout


def git_file_sets(project_root: Path) -> Tuple[Set[str], Set[str], Dict[str, str]]:
    metadata: Dict[str, str] = {}
    tracked: Set[str] = set()
    lfs: Set[str] = set()

    ok, output = run_process(["git", "ls-files", "-z"], project_root)
    metadata["git_available"] = str(ok)
    if ok:
        tracked = {item.replace("\\", "/") for item in output.split("\0") if item}

    ok_lfs, output_lfs = run_process(["git", "lfs", "ls-files", "-n"], project_root)
    metadata["git_lfs_available"] = str(ok_lfs)
    if ok_lfs:
        lfs = {line.strip().replace("\\", "/") for line in output_lfs.splitlines() if line.strip()}

    ok_commit, commit = run_process(["git", "rev-parse", "HEAD"], project_root)
    metadata["git_commit"] = commit.strip() if ok_commit else "unavailable"
    ok_branch, branch = run_process(["git", "branch", "--show-current"], project_root)
    metadata["git_branch"] = branch.strip() if ok_branch else "unavailable"
    return tracked, lfs, metadata


def area_for(relative: str) -> str:
    parts = Path(relative).parts
    if not parts:
        return "[root]"
    if len(parts) == 1:
        return "[root]"
    if parts[0].lower() == "assets" and len(parts) >= 2:
        return f"Assets/{parts[1]}"
    return parts[0]


def read_png_dimensions(path: Path) -> Tuple[Optional[int], Optional[int]]:
    try:
        with path.open("rb") as stream:
            header = stream.read(24)
        if len(header) >= 24 and header[:8] == b"\x89PNG\r\n\x1a\n" and header[12:16] == b"IHDR":
            return struct.unpack(">II", header[16:24])
    except OSError:
        pass
    return None, None


def read_jpeg_dimensions(path: Path) -> Tuple[Optional[int], Optional[int]]:
    try:
        with path.open("rb") as stream:
            if stream.read(2) != b"\xff\xd8":
                return None, None
            while True:
                marker_start = stream.read(1)
                if not marker_start:
                    break
                if marker_start != b"\xff":
                    continue
                marker = stream.read(1)
                while marker == b"\xff":
                    marker = stream.read(1)
                if not marker or marker in {b"\xd8", b"\xd9"}:
                    continue
                length_bytes = stream.read(2)
                if len(length_bytes) != 2:
                    break
                segment_length = struct.unpack(">H", length_bytes)[0]
                if segment_length < 2:
                    break
                if marker[0] in {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}:
                    data = stream.read(5)
                    if len(data) == 5:
                        height, width = struct.unpack(">HH", data[1:5])
                        return width, height
                    break
                stream.seek(segment_length - 2, os.SEEK_CUR)
    except (OSError, struct.error):
        pass
    return None, None


def image_dimensions(path: Path, extension: str) -> Tuple[Optional[int], Optional[int]]:
    if extension == ".png":
        return read_png_dimensions(path)
    if extension in {".jpg", ".jpeg"}:
        return read_jpeg_dimensions(path)
    return None, None


def read_text(path: Path, maximum_bytes: Optional[int] = None) -> Optional[str]:
    try:
        if maximum_bytes is not None and path.stat().st_size > maximum_bytes:
            return None
        return path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return None


def apply_texture_metadata(record: FileRecord, absolute_path: Path) -> None:
    meta_path = Path(f"{absolute_path}.meta")
    if not meta_path.exists():
        return
    text = read_text(meta_path, maximum_bytes=2 * 1024 * 1024)
    if text is None:
        return
    max_sizes = sorted({int(value) for value in MAX_TEXTURE_RE.findall(text)})
    record.importer_max_sizes = ",".join(str(value) for value in max_sizes)
    record.readable = "isReadable: 1" in text
    record.mipmaps_disabled = "enableMipMap: 0" in text
    record.streaming_mipmaps = "streamingMipmaps: 1" in text
    record.crunch = "crunchedCompression: 1" in text


def collect_file_records(
    project_root: Path,
    output_base: Path,
    tracked: Set[str],
    lfs: Set[str],
) -> Tuple[List[FileRecord], Dict[str, Path]]:
    records: List[FileRecord] = []
    absolute_by_relative: Dict[str, Path] = {}

    for path in walk_project_files(project_root, output_base):
        try:
            relative = normalize_relative(path, project_root)
            size = path.stat().st_size
        except (OSError, ValueError):
            continue
        extension = path.suffix.lower()
        record = FileRecord(
            path=relative,
            size_bytes=size,
            extension=extension or "[no extension]",
            area=area_for(relative),
            tracked=relative in tracked,
            lfs=relative in lfs,
        )
        if extension in IMAGE_EXTENSIONS:
            record.width, record.height = image_dimensions(path, extension)
        if extension in TEXTURE_EXTENSIONS and relative.startswith("Assets/"):
            apply_texture_metadata(record, path)
        records.append(record)
        absolute_by_relative[relative] = path

    return records, absolute_by_relative


def find_meta_issues(project_root: Path) -> List[MetaIssue]:
    assets = project_root / "Assets"
    if not assets.is_dir():
        return [MetaIssue("assets_directory_missing", "Assets")]

    issues: List[MetaIssue] = []
    for current, dirs, files in os.walk(assets):
        current_path = Path(current)
        dirs[:] = [name for name in dirs if name.lower() not in EXCLUDED_DIRECTORY_NAMES]

        for directory_name in dirs:
            directory = current_path / directory_name
            meta = Path(f"{directory}.meta")
            if not meta.exists():
                issues.append(MetaIssue("missing_folder_meta", normalize_relative(directory, project_root)))

        for file_name in files:
            file_path = current_path / file_name
            relative = normalize_relative(file_path, project_root)
            if file_name.endswith(".meta"):
                target = Path(str(file_path)[:-5])
                if not target.exists():
                    issues.append(MetaIssue("orphan_meta", relative))
            else:
                meta = Path(f"{file_path}.meta")
                if not meta.exists():
                    issues.append(MetaIssue("missing_file_meta", relative))
    return sorted(issues, key=lambda item: (item.issue, item.path.lower()))


def collect_guid_map(
    project_root: Path,
) -> Tuple[Dict[str, str], Dict[str, List[str]], List[Tuple[str, List[str]]]]:
    assets = project_root / "Assets"
    guid_to_asset: Dict[str, str] = {}
    asset_to_guids: Dict[str, List[str]] = defaultdict(list)

    if not assets.exists():
        return guid_to_asset, asset_to_guids, []

    for meta in assets.rglob("*.meta"):
        text = read_text(meta, maximum_bytes=2 * 1024 * 1024)
        if text is None:
            continue
        match = META_GUID_RE.search(text)
        if not match:
            continue
        guid = match.group(1).lower()
        asset_path = normalize_relative(Path(str(meta)[:-5]), project_root)
        asset_to_guids[asset_path].append(guid)
        if guid not in guid_to_asset:
            guid_to_asset[guid] = asset_path

    duplicates: List[Tuple[str, List[str]]] = []
    reverse: Dict[str, List[str]] = defaultdict(list)
    for asset_path, guids in asset_to_guids.items():
        for guid in guids:
            reverse[guid].append(asset_path)
    for guid, paths in reverse.items():
        if len(paths) > 1:
            duplicates.append((guid, sorted(paths)))
    return guid_to_asset, asset_to_guids, duplicates


def parse_enabled_build_scenes(project_root: Path) -> List[str]:
    settings = project_root / "ProjectSettings" / "EditorBuildSettings.asset"
    text = read_text(settings, maximum_bytes=5 * 1024 * 1024)
    if text is None:
        return []
    scenes: List[str] = []
    enabled: Optional[bool] = None
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("- enabled:"):
            enabled = stripped.split(":", 1)[1].strip() == "1"
        elif stripped.startswith("path:") and enabled is not None:
            path = stripped.split(":", 1)[1].strip().strip('"').replace("\\", "/")
            if enabled and path:
                scenes.append(path)
            enabled = None
    return scenes


def extract_known_asset_guids(text: str, guid_to_asset: Dict[str, str]) -> Set[str]:
    result: Set[str] = set()
    for guid in GUID_RE.findall(text):
        asset = guid_to_asset.get(guid.lower())
        if asset:
            result.add(asset)
    return result


def build_dependency_graph(
    project_root: Path,
    records_by_path: Dict[str, FileRecord],
    guid_to_asset: Dict[str, str],
) -> Tuple[Dict[str, Set[str]], Counter, Set[str], List[str]]:
    adjacency: Dict[str, Set[str]] = defaultdict(set)
    references = Counter()

    for relative, record in records_by_path.items():
        if not relative.startswith("Assets/") or record.extension not in UNITY_REFERENCE_EXTENSIONS:
            continue
        absolute = project_root / Path(relative)
        text = read_text(absolute, maximum_bytes=64 * 1024 * 1024)
        if text is None:
            continue
        targets = extract_known_asset_guids(text, guid_to_asset)
        targets.discard(relative)
        adjacency[relative].update(targets)
        references.update(targets)

    roots: Set[str] = set(parse_enabled_build_scenes(project_root))

    project_settings = project_root / "ProjectSettings"
    if project_settings.exists():
        for path in project_settings.rglob("*"):
            if not path.is_file() or path.stat().st_size > 32 * 1024 * 1024:
                continue
            text = read_text(path)
            if text:
                roots.update(extract_known_asset_guids(text, guid_to_asset))

    for relative in records_by_path:
        lower_parts = [part.lower() for part in Path(relative).parts]
        if "resources" in lower_parts or "streamingassets" in lower_parts:
            roots.add(relative)
        if "addressableassetsdata" in lower_parts:
            roots.add(relative)

    reachable: Set[str] = set()
    queue = deque(root for root in roots if root in records_by_path)
    while queue:
        current = queue.popleft()
        if current in reachable:
            continue
        reachable.add(current)
        for dependency in adjacency.get(current, ()):
            if dependency not in reachable:
                queue.append(dependency)

    return adjacency, references, reachable, parse_enabled_build_scenes(project_root)


def protected_reason(relative: str) -> Optional[str]:
    parts = [part.lower() for part in Path(relative).parts]
    if "resources" in parts:
        return "protected_resources_dynamic_load"
    if "streamingassets" in parts:
        return "protected_streaming_assets"
    if "addressableassetsdata" in parts:
        return "protected_addressables_configuration"
    if "editor" in parts:
        return "protected_editor_only"
    if "plugins" in parts:
        return "protected_plugin"
    return None


def assign_usage_states(
    records: List[FileRecord],
    reachable: Set[str],
    references: Counter,
    build_scenes: Sequence[str],
) -> List[FileRecord]:
    build_scene_set = set(build_scenes)
    candidates: List[FileRecord] = []
    for record in records:
        record.reference_count = int(references.get(record.path, 0))
        if not record.path.startswith("Assets/") or record.path.endswith(".meta"):
            record.usage_state = "not_unity_asset"
            continue
        if record.path in reachable:
            record.usage_state = "used_or_root"
            continue
        reason = protected_reason(record.path)
        if reason:
            record.usage_state = reason
            continue
        if record.extension in UNUSED_CANDIDATE_EXTENSIONS:
            if record.extension == ".unity" and record.path not in build_scene_set:
                record.usage_state = "candidate_scene_not_enabled"
            else:
                record.usage_state = "candidate_not_reachable"
            candidates.append(record)
        else:
            record.usage_state = "not_assessed_type"
    return sorted(candidates, key=lambda item: (-item.size_bytes, item.path.lower()))


def sha256_file(path: Path) -> Optional[str]:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            while True:
                chunk = stream.read(4 * 1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
        return digest.hexdigest()
    except OSError:
        return None


def find_duplicates(
    records: Sequence[FileRecord],
    absolute_by_relative: Dict[str, Path],
    minimum_bytes: int,
) -> List[DuplicateGroup]:
    by_size: Dict[int, List[FileRecord]] = defaultdict(list)
    for record in records:
        if record.size_bytes < minimum_bytes or record.path.endswith(".meta"):
            continue
        by_size[record.size_bytes].append(record)

    by_hash: Dict[Tuple[int, str], List[str]] = defaultdict(list)
    candidate_groups = [group for group in by_size.values() if len(group) > 1]
    total = sum(len(group) for group in candidate_groups)
    completed = 0
    for group in candidate_groups:
        for record in group:
            completed += 1
            if total and (completed == 1 or completed % 25 == 0 or completed == total):
                print(f"  Hash de duplicados: {completed}/{total}", flush=True)
            digest = sha256_file(absolute_by_relative[record.path])
            if digest:
                by_hash[(record.size_bytes, digest)].append(record.path)

    duplicates: List[DuplicateGroup] = []
    for (size, digest), paths in by_hash.items():
        if len(paths) < 2:
            continue
        duplicates.append(
            DuplicateGroup(
                sha256=digest,
                size_bytes_each=size,
                copies=len(paths),
                wasted_bytes=size * (len(paths) - 1),
                paths=sorted(paths, key=str.lower),
            )
        )
    return sorted(duplicates, key=lambda group: (-group.wasted_bytes, group.paths[0].lower()))


def format_bytes(value: int) -> str:
    units = ["B", "KB", "MB", "GB", "TB"]
    amount = float(value)
    for unit in units:
        if amount < 1024 or unit == units[-1]:
            return f"{amount:.2f} {unit}" if unit != "B" else f"{int(amount)} B"
        amount /= 1024
    return f"{value} B"


def csv_value(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "yes" if value else "no"
    return value


def write_csv(path: Path, headers: Sequence[str], rows: Iterable[Sequence[object]]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(headers)
        for row in rows:
            writer.writerow([csv_value(value) for value in row])


def markdown_escape(text: object) -> str:
    return str(text).replace("|", "\\|").replace("\n", " ")


def markdown_table(headers: Sequence[str], rows: Iterable[Sequence[object]]) -> str:
    lines = [
        "| " + " | ".join(markdown_escape(item) for item in headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(markdown_escape(item) for item in row) + " |")
    return "\n".join(lines)


def summarize_records(records: Sequence[FileRecord]) -> Tuple[Counter, Counter]:
    by_extension = Counter()
    by_area = Counter()
    for record in records:
        by_extension[record.extension] += record.size_bytes
        by_area[record.area] += record.size_bytes
    return by_extension, by_area


def write_reports(
    output_dir: Path,
    timestamp: str,
    project_root: Path,
    records: List[FileRecord],
    candidates: List[FileRecord],
    duplicates: List[DuplicateGroup],
    meta_issues: List[MetaIssue],
    duplicate_guids: List[Tuple[str, List[str]]],
    build_scenes: Sequence[str],
    git_metadata: Dict[str, str],
    large_threshold_bytes: int,
    top_count: int,
) -> Dict[str, Path]:
    stem = f"asset_audit_{timestamp}"
    report_paths = {
        "markdown": output_dir / f"{stem}.md",
        "json": output_dir / f"{stem}.json",
        "all_files": output_dir / f"{stem}_all_files.csv",
        "largest": output_dir / f"{stem}_largest_files.csv",
        "unused": output_dir / f"{stem}_possible_unused.csv",
        "duplicates": output_dir / f"{stem}_duplicates.csv",
        "meta": output_dir / f"{stem}_meta_issues.csv",
        "lfs": output_dir / f"{stem}_oversized_not_lfs.csv",
        "textures": output_dir / f"{stem}_large_textures.csv",
    }

    records.sort(key=lambda item: (-item.size_bytes, item.path.lower()))
    by_extension, by_area = summarize_records(records)
    total_bytes = sum(record.size_bytes for record in records)
    assets_bytes = sum(record.size_bytes for record in records if record.path.startswith("Assets/"))
    lfs_available = git_metadata.get("git_lfs_available") == "True"
    oversized_not_lfs = (
        [
            record
            for record in records
            if record.size_bytes >= large_threshold_bytes and record.tracked and not record.lfs
        ]
        if lfs_available
        else []
    )
    large_textures = [
        record
        for record in records
        if record.path.startswith("Assets/")
        and record.extension in TEXTURE_EXTENSIONS
        and ((record.width or 0) >= 4096 or (record.height or 0) >= 4096 or record.size_bytes >= large_threshold_bytes)
    ]

    write_csv(
        report_paths["all_files"],
        [
            "path",
            "size_bytes",
            "size_mb",
            "extension",
            "area",
            "tracked",
            "lfs",
            "width",
            "height",
            "importer_max_sizes",
            "readable",
            "mipmaps_disabled",
            "streaming_mipmaps",
            "crunch",
            "usage_state",
            "reference_count",
        ],
        (
            (
                record.path,
                record.size_bytes,
                f"{record.size_mb:.4f}",
                record.extension,
                record.area,
                record.tracked,
                record.lfs,
                record.width,
                record.height,
                record.importer_max_sizes,
                record.readable,
                record.mipmaps_disabled,
                record.streaming_mipmaps,
                record.crunch,
                record.usage_state,
                record.reference_count,
            )
            for record in records
        ),
    )
    write_csv(
        report_paths["largest"],
        ["path", "size_bytes", "size_mb", "extension", "area", "tracked", "lfs"],
        (
            (record.path, record.size_bytes, f"{record.size_mb:.4f}", record.extension, record.area, record.tracked, record.lfs)
            for record in records
        ),
    )
    write_csv(
        report_paths["unused"],
        ["path", "size_bytes", "size_mb", "extension", "usage_state", "reference_count"],
        (
            (record.path, record.size_bytes, f"{record.size_mb:.4f}", record.extension, record.usage_state, record.reference_count)
            for record in candidates
        ),
    )
    write_csv(
        report_paths["duplicates"],
        ["sha256", "size_bytes_each", "size_mb_each", "copies", "wasted_bytes", "wasted_mb", "paths"],
        (
            (
                group.sha256,
                group.size_bytes_each,
                f"{group.size_bytes_each / (1024 * 1024):.4f}",
                group.copies,
                group.wasted_bytes,
                f"{group.wasted_bytes / (1024 * 1024):.4f}",
                " | ".join(group.paths),
            )
            for group in duplicates
        ),
    )
    write_csv(report_paths["meta"], ["issue", "path"], ((issue.issue, issue.path) for issue in meta_issues))
    write_csv(
        report_paths["lfs"],
        ["path", "size_bytes", "size_mb", "extension"],
        ((record.path, record.size_bytes, f"{record.size_mb:.4f}", record.extension) for record in oversized_not_lfs),
    )
    write_csv(
        report_paths["textures"],
        [
            "path",
            "size_bytes",
            "size_mb",
            "width",
            "height",
            "importer_max_sizes",
            "readable",
            "mipmaps_disabled",
            "streaming_mipmaps",
            "crunch",
            "usage_state",
        ],
        (
            (
                record.path,
                record.size_bytes,
                f"{record.size_mb:.4f}",
                record.width,
                record.height,
                record.importer_max_sizes,
                record.readable,
                record.mipmaps_disabled,
                record.streaming_mipmaps,
                record.crunch,
                record.usage_state,
            )
            for record in large_textures
        ),
    )

    summary = {
        "generated_at": datetime.now().astimezone().isoformat(timespec="seconds"),
        "project_root": str(project_root),
        "git": git_metadata,
        "enabled_build_scenes": list(build_scenes),
        "counts": {
            "files": len(records),
            "unity_asset_files": sum(1 for record in records if record.path.startswith("Assets/") and not record.path.endswith(".meta")),
            "possible_unused_candidates": len(candidates),
            "duplicate_groups": len(duplicates),
            "meta_issues": len(meta_issues),
            "duplicate_guids": len(duplicate_guids),
            "oversized_tracked_not_lfs": len(oversized_not_lfs),
            "large_textures": len(large_textures),
        },
        "sizes": {
            "project_scanned_bytes": total_bytes,
            "assets_bytes": assets_bytes,
            "duplicate_wasted_bytes": sum(group.wasted_bytes for group in duplicates),
        },
        "largest_files": [asdict(record) for record in records[:top_count]],
        "largest_unused_candidates": [asdict(record) for record in candidates[:top_count]],
        "largest_duplicate_groups": [asdict(group) for group in duplicates[:top_count]],
        "meta_issues": [asdict(issue) for issue in meta_issues[:top_count]],
        "duplicate_guids": [{"guid": guid, "paths": paths} for guid, paths in duplicate_guids[:top_count]],
        "reports": {name: path.name for name, path in report_paths.items()},
    }
    report_paths["json"].write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")

    md: List[str] = []
    md.append("# Auditoría estática de assets")
    md.append("")
    md.append(f"- **Fecha:** {summary['generated_at']}")
    md.append(f"- **Proyecto:** `{project_root}`")
    md.append(f"- **Commit:** `{git_metadata.get('git_commit', 'unavailable')}`")
    md.append(f"- **Rama:** `{git_metadata.get('git_branch', 'unavailable')}`")
    md.append("- **Naturaleza:** análisis de archivos y referencias; no mide rendimiento en ejecución.")
    md.append("")
    md.append("## Resumen")
    md.append("")
    md.append(
        markdown_table(
            ["Indicador", "Resultado"],
            [
                ("Archivos analizados", len(records)),
                ("Tamaño analizado", format_bytes(total_bytes)),
                ("Tamaño bajo Assets", format_bytes(assets_bytes)),
                ("Candidatos posiblemente sin uso", len(candidates)),
                ("Grupos de duplicados exactos", len(duplicates)),
                ("Espacio duplicado potencial", format_bytes(sum(group.wasted_bytes for group in duplicates))),
                ("Problemas de .meta", len(meta_issues)),
                ("GUID duplicados", len(duplicate_guids)),
                (f"Archivos >= {large_threshold_bytes / (1024 * 1024):.1f} MB fuera de LFS", len(oversized_not_lfs)),
                ("Texturas grandes o >= 4K detectables", len(large_textures)),
            ],
        )
    )
    md.append("")
    md.append("## Escenas activas del build")
    md.append("")
    if build_scenes:
        md.extend(f"- `{scene}`" for scene in build_scenes)
    else:
        md.append("No se pudieron identificar escenas activas.")

    md.append("")
    md.append("## Tamaño por área")
    md.append("")
    md.append(
        markdown_table(
            ["Área", "Tamaño"],
            ((area, format_bytes(size)) for area, size in by_area.most_common(30)),
        )
    )
    md.append("")
    md.append("## Tamaño por extensión")
    md.append("")
    md.append(
        markdown_table(
            ["Extensión", "Tamaño"],
            ((extension, format_bytes(size)) for extension, size in by_extension.most_common(30)),
        )
    )
    md.append("")
    md.append(f"## {min(top_count, len(records))} archivos más pesados")
    md.append("")
    md.append(
        markdown_table(
            ["Archivo", "Tamaño", "LFS"],
            ((f"`{record.path}`", format_bytes(record.size_bytes), "sí" if record.lfs else "no") for record in records[:top_count]),
        )
    )

    md.append("")
    md.append("## Duplicados exactos")
    md.append("")
    if duplicates:
        md.append(
            markdown_table(
                ["Copias", "Cada archivo", "Ahorro potencial", "Rutas"],
                (
                    (
                        group.copies,
                        format_bytes(group.size_bytes_each),
                        format_bytes(group.wasted_bytes),
                        "<br>".join(f"`{path}`" for path in group.paths),
                    )
                    for group in duplicates[:top_count]
                ),
            )
        )
    else:
        md.append("No se detectaron duplicados en el umbral configurado, o se omitió el cálculo.")

    md.append("")
    md.append("## Candidatos posiblemente sin uso")
    md.append("")
    md.append(
        "Un candidato no es un archivo seguro para borrar. Unity puede cargar contenido por nombre, código, Resources, StreamingAssets o sistemas externos. Revisar manualmente antes de eliminar."
    )
    md.append("")
    if candidates:
        md.append(
            markdown_table(
                ["Archivo", "Tamaño", "Motivo", "Referencias conocidas"],
                (
                    (f"`{record.path}`", format_bytes(record.size_bytes), record.usage_state, record.reference_count)
                    for record in candidates[:top_count]
                ),
            )
        )
    else:
        md.append("No se generaron candidatos.")

    md.append("")
    md.append("## Texturas grandes")
    md.append("")
    if large_textures:
        md.append(
            markdown_table(
                ["Archivo", "Fuente", "Resolución", "Max Size importador", "Mipmaps", "Streaming"],
                (
                    (
                        f"`{record.path}`",
                        format_bytes(record.size_bytes),
                        f"{record.width or '?'}x{record.height or '?'}",
                        record.importer_max_sizes or "?",
                        "off" if record.mipmaps_disabled else "on/unknown",
                        "on" if record.streaming_mipmaps else "off/unknown",
                    )
                    for record in large_textures[:top_count]
                ),
            )
        )
    else:
        md.append("No se detectaron texturas grandes mediante los formatos inspeccionables.")

    md.append("")
    md.append("## Archivos grandes versionados fuera de Git LFS")
    md.append("")
    if oversized_not_lfs:
        md.append(
            markdown_table(
                ["Archivo", "Tamaño"],
                ((f"`{record.path}`", format_bytes(record.size_bytes)) for record in oversized_not_lfs[:top_count]),
            )
        )
    else:
        md.append("No se detectaron archivos grandes versionados fuera de LFS, o Git LFS no está disponible.")

    md.append("")
    md.append("## Problemas de archivos .meta")
    md.append("")
    if meta_issues:
        md.append(
            markdown_table(
                ["Problema", "Ruta"],
                ((issue.issue, f"`{issue.path}`") for issue in meta_issues[:top_count]),
            )
        )
    else:
        md.append("No se detectaron archivos .meta ausentes o huérfanos.")

    if duplicate_guids:
        md.append("")
        md.append("## GUID duplicados")
        md.append("")
        md.append(
            markdown_table(
                ["GUID", "Rutas"],
                ((guid, "<br>".join(f"`{path}`" for path in paths)) for guid, paths in duplicate_guids[:top_count]),
            )
        )

    md.append("")
    md.append("## Archivos generados")
    md.append("")
    for name, path in report_paths.items():
        md.append(f"- **{name}:** `{path.name}`")
    md.append("")
    md.append("## Limitaciones")
    md.append("")
    md.extend(
        [
            "- El análisis de uso interpreta GUID de archivos YAML de Unity y referencias de ProjectSettings.",
            "- No puede confirmar referencias creadas dinámicamente mediante strings, reflexión, AssetBundles o código personalizado.",
            "- Resources, StreamingAssets, Plugins, Editor y Addressables se protegen para reducir falsos positivos.",
            "- El tamaño del archivo fuente no equivale al tamaño dentro del build ni a la memoria de GPU.",
            "- La auditoría no elimina, mueve ni modifica assets.",
        ]
    )
    # UTF-8 with BOM keeps accents readable in Windows PowerShell/Notepad as
    # well as in GitHub-compatible Markdown viewers.
    report_paths["markdown"].write_text("\n".join(md) + "\n", encoding="utf-8-sig")
    return report_paths


def main() -> int:
    args = parse_args()
    project_root = args.project.resolve()
    if not (project_root / "Assets").is_dir() or not (project_root / "ProjectSettings").is_dir():
        print(f"ERROR: no parece un proyecto Unity válido: {project_root}", file=sys.stderr)
        return 2

    output_base = (args.output.resolve() if args.output else project_root / REPORTS_DIRECTORY)
    timestamp = datetime.now().astimezone().strftime("%Y-%m-%d_%H-%M-%S")
    output_dir = make_output_directory(output_base, timestamp)

    print("Auditoría de assets - Fatto Prizzerva Lightning")
    print(f"Proyecto: {project_root}")
    print(f"Salida:   {output_dir}")
    print("1/7 Leyendo estado de Git y Git LFS...", flush=True)
    tracked, lfs, git_metadata = git_file_sets(project_root)
    print("2/7 Inventariando archivos...", flush=True)
    records, absolute_by_relative = collect_file_records(project_root, output_base, tracked, lfs)
    records_by_path = {record.path: record for record in records}
    print(f"    {len(records)} archivos encontrados.")

    print("3/7 Revisando archivos .meta y GUID...", flush=True)
    meta_issues = find_meta_issues(project_root)
    guid_to_asset, _, duplicate_guids = collect_guid_map(project_root)

    print("4/7 Construyendo grafo de dependencias...", flush=True)
    _, references, reachable, build_scenes = build_dependency_graph(project_root, records_by_path, guid_to_asset)
    candidates = assign_usage_states(records, reachable, references, build_scenes)

    print("5/7 Buscando duplicados exactos...", flush=True)
    duplicates: List[DuplicateGroup] = []
    if not args.skip_duplicates:
        duplicates = find_duplicates(
            records,
            absolute_by_relative,
            max(0, args.duplicate_min_kb) * 1024,
        )
    else:
        print("    Omitido mediante --skip-duplicates.")

    print("6/7 Generando CSV, JSON y Markdown...", flush=True)
    report_paths = write_reports(
        output_dir=output_dir,
        timestamp=timestamp,
        project_root=project_root,
        records=records,
        candidates=candidates,
        duplicates=duplicates,
        meta_issues=meta_issues,
        duplicate_guids=duplicate_guids,
        build_scenes=build_scenes,
        git_metadata=git_metadata,
        large_threshold_bytes=max(0, int(args.large_mb * 1024 * 1024)),
        top_count=max(10, args.top),
    )

    print("7/7 Auditoría completada.")
    print(f"Informe principal: {report_paths['markdown']}")
    print(f"Carpeta completa:  {output_dir}")
    print("No se ha eliminado ni modificado ningún asset.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
