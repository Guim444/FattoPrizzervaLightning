#!/usr/bin/env python3
"""Post-process raw Fatto Prizzerva build telemetry.

The Unity player writes only fixed raw samples and events. Every aggregate in this
module is intentionally calculated after the player has stopped.
"""

from __future__ import annotations

import argparse
import csv
import html
import json
import math
import statistics
import struct
import sys
import tempfile
from collections import defaultdict, namedtuple
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


MAGIC = b"FBTL"
FORMAT_VERSION = 1
HEADER = struct.Struct("<4sii")
FRAME_STRUCT = struct.Struct("<qdff15q10i")

FRAME_FIELDS = [
    "frame_index",
    "realtime_seconds",
    "unscaled_delta_seconds",
    "time_scale",
    "main_thread_ns",
    "render_thread_ns",
    "gpu_frame_ns",
    "gc_allocated_bytes",
    "system_used_memory_bytes",
    "total_used_memory_bytes",
    "total_reserved_memory_bytes",
    "gfx_used_memory_bytes",
    "texture_memory_bytes",
    "mesh_memory_bytes",
    "draw_calls",
    "batches",
    "set_pass_calls",
    "triangles",
    "vertices",
    "gc_gen0",
    "gc_gen1",
    "gc_gen2",
    "scene_build_index",
    "quality_level",
    "width",
    "height",
    "target_frame_rate",
    "v_sync_count",
    "validity_flags",
]
Frame = namedtuple("Frame", FRAME_FIELDS)

COUNTER_BITS = {
    "main_thread_ns": 0,
    "render_thread_ns": 1,
    "gpu_frame_ns": 2,
    "gc_allocated_bytes": 3,
    "system_used_memory_bytes": 4,
    "total_used_memory_bytes": 5,
    "total_reserved_memory_bytes": 6,
    "gfx_used_memory_bytes": 7,
    "texture_memory_bytes": 8,
    "mesh_memory_bytes": 9,
    "draw_calls": 10,
    "batches": 11,
    "set_pass_calls": 12,
    "triangles": 13,
    "vertices": 14,
}

MIB = 1024.0 * 1024.0


@dataclass
class Session:
    path: Path
    metadata: dict[str, Any]
    frames: list[Frame]
    events: list[dict[str, Any]]
    session_end: dict[str, Any]
    complete: bool
    warnings: list[str]

    @property
    def name(self) -> str:
        return self.path.name


def load_json(path: Path, fallback: Any) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, json.JSONDecodeError):
        return fallback


def read_frames(path: Path) -> tuple[list[Frame], list[str]]:
    warnings: list[str] = []
    frames: list[Frame] = []
    try:
        with path.open("rb") as handle:
            header_data = handle.read(HEADER.size)
            if len(header_data) != HEADER.size:
                raise ValueError("La cabecera de frames.bin está incompleta.")
            magic, version, record_size = HEADER.unpack(header_data)
            if magic != MAGIC:
                raise ValueError(f"Firma binaria no reconocida: {magic!r}.")
            if version != FORMAT_VERSION:
                raise ValueError(f"Versión binaria {version}; se esperaba {FORMAT_VERSION}.")
            if record_size != FRAME_STRUCT.size:
                raise ValueError(
                    f"Tamaño de registro {record_size}; se esperaba {FRAME_STRUCT.size}."
                )

            while True:
                data = handle.read(record_size)
                if not data:
                    break
                if len(data) != record_size:
                    warnings.append(
                        f"Se ignoraron {len(data)} bytes finales de un registro incompleto."
                    )
                    break
                frames.append(Frame(*FRAME_STRUCT.unpack(data)))
    except OSError as exc:
        raise ValueError(f"No se puede leer {path}: {exc}") from exc
    return frames, warnings


def read_events(path: Path) -> tuple[list[dict[str, Any]], list[str]]:
    if not path.exists():
        return [], ["La sesión no contiene events.jsonl."]
    events: list[dict[str, Any]] = []
    warnings: list[str] = []
    with path.open("r", encoding="utf-8", errors="replace") as handle:
        for line_number, line in enumerate(handle, 1):
            if not line.strip():
                continue
            try:
                events.append(json.loads(line))
            except json.JSONDecodeError:
                warnings.append(f"Evento JSON inválido en la línea {line_number}.")
    return events, warnings


def load_session(path: Path) -> Session:
    metadata = load_json(path / "session.json", {})
    if not metadata:
        raise ValueError(f"Falta session.json o no es válido en {path}.")
    frames, frame_warnings = read_frames(path / "frames.bin")
    events, event_warnings = read_events(path / "events.jsonl")
    session_end = load_json(path / "session_end.json", {})
    warnings = frame_warnings + event_warnings
    complete = (path / "complete.flag").exists()
    if not complete:
        warnings.append("La sesión no tiene complete.flag; pudo cerrarse de forma abrupta.")
    if not frames:
        warnings.append("La sesión no contiene muestras de frame.")
    if session_end.get("droppedFrameSamples", 0):
        warnings.append(
            f"Se descartaron {session_end['droppedFrameSamples']} muestras por saturación de escritura."
        )
    if session_end.get("droppedEvents", 0):
        warnings.append(
            f"Se descartaron {session_end['droppedEvents']} eventos por saturación de escritura."
        )
    if session_end.get("writerError"):
        warnings.append(f"Error del escritor: {session_end['writerError']}")
    return Session(path, metadata, frames, events, session_end, complete, warnings)


def discover_sessions(root: Path) -> list[Path]:
    if not root.exists():
        return []
    candidates = [
        item
        for item in root.iterdir()
        if item.is_dir() and (item / "session.json").exists() and (item / "frames.bin").exists()
    ]
    return sorted(candidates, key=lambda item: item.stat().st_mtime)


def select_session(paths: Sequence[Path], selector: str | None) -> Path:
    if not paths:
        raise ValueError("No hay sesiones crudas para analizar.")
    if not selector:
        return paths[-1]

    selector_path = Path(selector).expanduser()
    if selector_path.is_dir():
        return selector_path.resolve()
    matches = [path for path in paths if selector.lower() in path.name.lower()]
    if len(matches) == 1:
        return matches[0]
    if not matches:
        raise ValueError(f"No se encuentra una sesión que coincida con '{selector}'.")
    raise ValueError(f"El selector '{selector}' coincide con varias sesiones.")


def percentile(values: Sequence[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    if len(ordered) == 1:
        return float(ordered[0])
    position = (len(ordered) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return float(ordered[lower])
    weight = position - lower
    return float(ordered[lower] * (1.0 - weight) + ordered[upper] * weight)


def stats(values: Iterable[float], include_sum: bool = False) -> dict[str, Any]:
    cleaned = [float(value) for value in values if math.isfinite(float(value))]
    if not cleaned:
        return {"count": 0}
    result: dict[str, Any] = {
        "count": len(cleaned),
        "min": min(cleaned),
        "mean": statistics.fmean(cleaned),
        "median": statistics.median(cleaned),
        "p95": percentile(cleaned, 0.95),
        "p99": percentile(cleaned, 0.99),
        "max": max(cleaned),
    }
    if include_sum:
        result["sum"] = math.fsum(cleaned)
    return result


def valid_counter_values(
    frames: Sequence[Frame], field: str, scale: float = 1.0, positive_only: bool = False
) -> list[float]:
    bit = COUNTER_BITS[field]
    values: list[float] = []
    for frame in frames:
        if not (frame.validity_flags & (1 << bit)):
            continue
        value = getattr(frame, field)
        if positive_only and value <= 0:
            continue
        if not positive_only and value < 0:
            continue
        values.append(value / scale)
    return values


def collection_delta(frames: Sequence[Frame], field: str) -> int | None:
    values = [getattr(frame, field) for frame in frames if getattr(frame, field) >= 0]
    if len(values) < 2:
        return None
    return max(0, values[-1] - values[0])


def frame_time_stats(frames: Sequence[Frame]) -> dict[str, Any]:
    milliseconds = [
        frame.unscaled_delta_seconds * 1000.0
        for frame in frames
        if frame.unscaled_delta_seconds > 0 and math.isfinite(frame.unscaled_delta_seconds)
    ]
    result = stats(milliseconds)
    if not milliseconds:
        return result

    mean_ms = result["mean"]
    one_count = max(1, math.ceil(len(milliseconds) * 0.01))
    point_one_count = max(1, math.ceil(len(milliseconds) * 0.001))
    slowest = sorted(milliseconds, reverse=True)
    result.update(
        {
            "average_fps": 1000.0 / mean_ms if mean_ms > 0 else None,
            "one_percent_low_fps": 1000.0 / statistics.fmean(slowest[:one_count]),
            "point_one_percent_low_fps": 1000.0
            / statistics.fmean(slowest[:point_one_count]),
            "hitches_over_33_33_ms": sum(value > 33.333333 for value in milliseconds),
            "hitches_over_50_ms": sum(value > 50.0 for value in milliseconds),
            "hitches_over_100_ms": sum(value > 100.0 for value in milliseconds),
        }
    )
    duration_seconds = math.fsum(milliseconds) / 1000.0
    result["hitches_over_50_per_minute"] = (
        result["hitches_over_50_ms"] * 60.0 / duration_seconds if duration_seconds else 0.0
    )
    return result


def analysis_window(
    frames: Sequence[Frame], warmup_seconds: float
) -> tuple[list[Frame], list[str]]:
    if not frames or warmup_seconds <= 0:
        return list(frames), []
    threshold = frames[0].realtime_seconds + warmup_seconds
    selected = [frame for frame in frames if frame.realtime_seconds >= threshold]
    if len(selected) >= 30:
        return selected, []
    return list(frames), [
        "La sesión era demasiado corta para excluir el calentamiento; se analizaron todos los frames."
    ]


def duration_seconds(frames: Sequence[Frame]) -> float:
    if len(frames) < 2:
        return 0.0
    return max(0.0, frames[-1].realtime_seconds - frames[0].realtime_seconds)


def scene_names(session: Session) -> dict[int, str]:
    names: dict[int, str] = {}
    for event in session.events:
        index = event.get("sceneBuildIndex")
        if not isinstance(index, int) or index < 0:
            continue
        name = event.get("name") or Path(str(event.get("scenePath", ""))).stem
        if name and event.get("eventType") in {"scene_loaded", "session_start"}:
            names[index] = str(name)
    return names


def calculate_session(session: Session, warmup_seconds: float) -> dict[str, Any]:
    frames, window_warnings = analysis_window(session.frames, warmup_seconds)
    session.warnings.extend(window_warnings)

    memory_fields = {
        "system_used_mib": "system_used_memory_bytes",
        "total_used_mib": "total_used_memory_bytes",
        "total_reserved_mib": "total_reserved_memory_bytes",
        "gfx_used_mib": "gfx_used_memory_bytes",
        "texture_mib": "texture_memory_bytes",
        "mesh_mib": "mesh_memory_bytes",
    }
    rendering_fields = {
        "draw_calls": "draw_calls",
        "batches": "batches",
        "set_pass_calls": "set_pass_calls",
        "triangles": "triangles",
        "vertices": "vertices",
    }

    per_scene_frames: dict[int, list[Frame]] = defaultdict(list)
    for frame in frames:
        per_scene_frames[frame.scene_build_index].append(frame)
    names = scene_names(session)
    per_scene = []
    for index, grouped in sorted(per_scene_frames.items()):
        per_scene.append(
            {
                "scene_build_index": index,
                "scene_name": names.get(index, "Sin escena" if index < 0 else f"Escena {index}"),
                "duration_seconds": duration_seconds(grouped),
                "frame_time_ms": frame_time_stats(grouped),
            }
        )

    result: dict[str, Any] = {
        "session_name": session.name,
        "session_path": str(session.path.resolve()),
        "metadata": session.metadata,
        "integrity": {
            "complete": session.complete,
            "warnings": list(dict.fromkeys(session.warnings)),
            "session_end": session.session_end,
        },
        "capture": {
            "raw_frame_count": len(session.frames),
            "raw_duration_seconds": duration_seconds(session.frames),
            "warmup_excluded_seconds": warmup_seconds,
            "analyzed_frame_count": len(frames),
            "analyzed_duration_seconds": duration_seconds(frames),
        },
        "all_frames": {"frame_time_ms": frame_time_stats(session.frames)},
        "frame_time_ms": frame_time_stats(frames),
        "cpu_gpu_ms": {
            "main_thread": stats(valid_counter_values(frames, "main_thread_ns", 1_000_000.0, True)),
            "render_thread": stats(valid_counter_values(frames, "render_thread_ns", 1_000_000.0, True)),
            "gpu_frame": stats(valid_counter_values(frames, "gpu_frame_ns", 1_000_000.0, True)),
        },
        "memory": {
            label: stats(valid_counter_values(frames, field, MIB))
            for label, field in memory_fields.items()
        },
        "rendering": {
            label: stats(valid_counter_values(frames, field))
            for label, field in rendering_fields.items()
        },
        "gc": {
            "allocated_mib_per_frame": stats(
                valid_counter_values(frames, "gc_allocated_bytes", MIB), include_sum=True
            ),
            "collections_gen0": collection_delta(frames, "gc_gen0"),
            "collections_gen1": collection_delta(frames, "gc_gen1"),
            "collections_gen2": collection_delta(frames, "gc_gen2"),
        },
        "per_scene": per_scene,
    }
    return result


COMPATIBILITY_FIELDS = [
    ("platform", "plataforma"),
    ("processorType", "CPU"),
    ("graphicsDeviceName", "GPU"),
    ("qualityName", "calidad"),
    ("width", "anchura"),
    ("height", "altura"),
]


def compatibility_differences(
    current: dict[str, Any], baseline: dict[str, Any]
) -> list[str]:
    differences = []
    for field, label in COMPATIBILITY_FIELDS:
        if current.get(field) != baseline.get(field):
            differences.append(
                f"{label}: {baseline.get(field, 'desconocido')} → {current.get(field, 'desconocido')}"
            )
    return differences


def find_automatic_baseline(
    paths: Sequence[Path], current_path: Path, current_metadata: dict[str, Any]
) -> Path | None:
    current_resolved = current_path.resolve()
    current_position = next(
        (index for index, path in enumerate(paths) if path.resolve() == current_resolved),
        len(paths),
    )
    previous = paths[:current_position]
    for path in reversed(previous):
        metadata = load_json(path / "session.json", {})
        if metadata and not compatibility_differences(current_metadata, metadata):
            return path
    return None


def nested_value(data: dict[str, Any], path: str) -> float | None:
    value: Any = data
    for part in path.split("."):
        if not isinstance(value, dict) or part not in value:
            return None
        value = value[part]
    return float(value) if isinstance(value, (int, float)) else None


COMPARISON_METRICS = [
    ("frame_time_ms.mean", "Frame medio", "ms", "lower"),
    ("frame_time_ms.p95", "Frame p95", "ms", "lower"),
    ("frame_time_ms.p99", "Frame p99", "ms", "lower"),
    ("frame_time_ms.one_percent_low_fps", "1% low", "FPS", "higher"),
    ("cpu_gpu_ms.main_thread.p95", "CPU principal p95", "ms", "lower"),
    ("cpu_gpu_ms.render_thread.p95", "Render thread p95", "ms", "lower"),
    ("cpu_gpu_ms.gpu_frame.p95", "GPU p95", "ms", "lower"),
    ("memory.total_used_mib.max", "Memoria Unity pico", "MiB", "lower"),
    ("memory.gfx_used_mib.max", "Memoria gráfica pico", "MiB", "lower"),
    ("rendering.draw_calls.mean", "Draw calls medios", "count", "lower"),
]


def compare_results(current: dict[str, Any], baseline: dict[str, Any]) -> list[dict[str, Any]]:
    rows = []
    for path, label, unit, direction in COMPARISON_METRICS:
        current_value = nested_value(current, path)
        baseline_value = nested_value(baseline, path)
        if current_value is None or baseline_value is None or baseline_value == 0:
            continue
        delta = (current_value - baseline_value) * 100.0 / abs(baseline_value)
        regression = delta > 5.0 if direction == "lower" else delta < -5.0
        improvement = delta < -5.0 if direction == "lower" else delta > 5.0
        rows.append(
            {
                "metric": label,
                "unit": unit,
                "baseline": baseline_value,
                "current": current_value,
                "delta_percent": delta,
                "status": "regression" if regression else "improvement" if improvement else "stable",
            }
        )
    return rows


def fmt(value: Any, digits: int = 2, suffix: str = "") -> str:
    if value is None:
        return "N/D"
    if isinstance(value, bool):
        return "Sí" if value else "No"
    if isinstance(value, int):
        return f"{value:,}".replace(",", ".") + suffix
    if isinstance(value, float):
        return f"{value:,.{digits}f}".replace(",", "X").replace(".", ",").replace("X", ".") + suffix
    return str(value)


def stat_rows(result: dict[str, Any]) -> list[tuple[str, str, dict[str, Any]]]:
    return [
        ("Frame time", "ms", result["frame_time_ms"]),
        ("CPU principal", "ms", result["cpu_gpu_ms"]["main_thread"]),
        ("Render thread", "ms", result["cpu_gpu_ms"]["render_thread"]),
        ("GPU frame", "ms", result["cpu_gpu_ms"]["gpu_frame"]),
        ("Memoria del sistema", "MiB", result["memory"]["system_used_mib"]),
        ("Memoria Unity usada", "MiB", result["memory"]["total_used_mib"]),
        ("Memoria Unity reservada", "MiB", result["memory"]["total_reserved_mib"]),
        ("Memoria gráfica", "MiB", result["memory"]["gfx_used_mib"]),
        ("Memoria de texturas", "MiB", result["memory"]["texture_mib"]),
        ("Memoria de mallas", "MiB", result["memory"]["mesh_mib"]),
        ("GC asignado por frame", "MiB", result["gc"]["allocated_mib_per_frame"]),
        ("Draw calls", "count", result["rendering"]["draw_calls"]),
        ("Batches", "count", result["rendering"]["batches"]),
        ("SetPass", "count", result["rendering"]["set_pass_calls"]),
        ("Triángulos", "count", result["rendering"]["triangles"]),
        ("Vértices", "count", result["rendering"]["vertices"]),
    ]


def write_summary_csv(path: Path, result: dict[str, Any]) -> None:
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["metric", "unit", "count", "min", "mean", "median", "p95", "p99", "max", "sum"],
        )
        writer.writeheader()
        for name, unit, values in stat_rows(result):
            writer.writerow(
                {"metric": name, "unit": unit, **{key: values.get(key, "") for key in writer.fieldnames[2:]}}
            )


def write_scene_csv(path: Path, result: dict[str, Any]) -> None:
    fields = [
        "scene_build_index",
        "scene_name",
        "duration_seconds",
        "frame_count",
        "average_fps",
        "one_percent_low_fps",
        "mean_frame_ms",
        "p95_frame_ms",
        "p99_frame_ms",
        "max_frame_ms",
        "hitches_over_50_ms",
    ]
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for scene in result["per_scene"]:
            frame = scene["frame_time_ms"]
            writer.writerow(
                {
                    "scene_build_index": scene["scene_build_index"],
                    "scene_name": scene["scene_name"],
                    "duration_seconds": scene["duration_seconds"],
                    "frame_count": frame.get("count", 0),
                    "average_fps": frame.get("average_fps", ""),
                    "one_percent_low_fps": frame.get("one_percent_low_fps", ""),
                    "mean_frame_ms": frame.get("mean", ""),
                    "p95_frame_ms": frame.get("p95", ""),
                    "p99_frame_ms": frame.get("p99", ""),
                    "max_frame_ms": frame.get("max", ""),
                    "hitches_over_50_ms": frame.get("hitches_over_50_ms", ""),
                }
            )


def svg_frame_chart(frames: Sequence[Frame]) -> str:
    points = [
        (frame.realtime_seconds, frame.unscaled_delta_seconds * 1000.0)
        for frame in frames
        if frame.unscaled_delta_seconds > 0
    ]
    if len(points) < 2:
        return "<p class='muted'>No hay muestras suficientes para dibujar la gráfica.</p>"

    max_points = 1100
    if len(points) > max_points:
        bucket_size = math.ceil(len(points) / max_points)
        reduced = []
        for start in range(0, len(points), bucket_size):
            bucket = points[start : start + bucket_size]
            reduced.append(max(bucket, key=lambda item: item[1]))
        points = reduced

    values = [value for _, value in points]
    y_limit = max(33.333, (percentile(values, 0.995) or max(values)) * 1.25)
    width, height = 1000.0, 280.0
    left, top, right, bottom = 58.0, 18.0, 18.0, 36.0
    plot_width = width - left - right
    plot_height = height - top - bottom
    start_time, end_time = points[0][0], points[-1][0]
    time_span = max(end_time - start_time, 0.001)

    coords = []
    for timestamp, value in points:
        x = left + (timestamp - start_time) / time_span * plot_width
        y = top + (1.0 - min(value, y_limit) / y_limit) * plot_height
        coords.append(f"{x:.1f},{y:.1f}")

    guides = []
    for threshold, label in [(16.667, "60 FPS"), (33.333, "30 FPS"), (50.0, "50 ms")]:
        if threshold > y_limit:
            continue
        y = top + (1.0 - threshold / y_limit) * plot_height
        guides.append(
            f"<line x1='{left}' y1='{y:.1f}' x2='{width-right}' y2='{y:.1f}' class='guide'/>"
            f"<text x='4' y='{y+4:.1f}' class='axis'>{html.escape(label)}</text>"
        )
    return (
        f"<svg viewBox='0 0 {width:.0f} {height:.0f}' role='img' aria-label='Tiempo de frame'>"
        + "".join(guides)
        + f"<polyline points='{' '.join(coords)}' class='frame-line'/><text x='{left}' y='{height-8}' class='axis'>0 s</text>"
        + f"<text x='{width-right-70}' y='{height-8}' class='axis'>{time_span:.1f} s</text>"
        + f"<text x='{left}' y='12' class='axis'>Escala hasta {y_limit:.1f} ms (picos mayores se recortan visualmente)</text></svg>"
    )


def html_stats_table(rows: Sequence[tuple[str, str, dict[str, Any]]]) -> str:
    body = []
    for name, unit, values in rows:
        body.append(
            "<tr>"
            f"<td>{html.escape(name)}</td><td>{html.escape(unit)}</td>"
            f"<td>{fmt(values.get('mean'))}</td><td>{fmt(values.get('median'))}</td>"
            f"<td>{fmt(values.get('p95'))}</td><td>{fmt(values.get('p99'))}</td>"
            f"<td>{fmt(values.get('max'))}</td><td>{fmt(values.get('count'))}</td>"
            "</tr>"
        )
    return (
        "<table><thead><tr><th>Métrica</th><th>Unidad</th><th>Media</th><th>Mediana</th>"
        "<th>p95</th><th>p99</th><th>Máximo</th><th>Muestras</th></tr></thead><tbody>"
        + "".join(body)
        + "</tbody></table>"
    )


def create_markdown(
    result: dict[str, Any], baseline_result: dict[str, Any] | None, comparison: list[dict[str, Any]]
) -> str:
    metadata = result["metadata"]
    frame = result["frame_time_ms"]
    lines = [
        "# Informe de telemetría de build",
        "",
        f"- Sesión: `{result['session_name']}`",
        f"- Commit/rama: `{metadata.get('gitCommit', 'unknown')}` / `{metadata.get('gitBranch', 'unknown')}`"
        + (" (con cambios sin commit)" if metadata.get("gitDirty") else ""),
        f"- Calidad y resolución: `{metadata.get('qualityName', 'N/D')}` · {metadata.get('width', 'N/D')}×{metadata.get('height', 'N/D')}",
        f"- Hardware: {metadata.get('processorType', 'N/D')} · {metadata.get('graphicsDeviceName', 'N/D')}",
        f"- Ventana analizada: {fmt(result['capture']['analyzed_duration_seconds'])} s, {fmt(result['capture']['analyzed_frame_count'])} frames; calentamiento excluido: {fmt(result['capture']['warmup_excluded_seconds'])} s.",
        "",
        "## Resultado principal",
        "",
        f"- FPS medios: {fmt(frame.get('average_fps'))}",
        f"- 1% low / 0,1% low: {fmt(frame.get('one_percent_low_fps'))} / {fmt(frame.get('point_one_percent_low_fps'))} FPS",
        f"- Frame medio / p95 / p99 / máximo: {fmt(frame.get('mean'))} / {fmt(frame.get('p95'))} / {fmt(frame.get('p99'))} / {fmt(frame.get('max'))} ms",
        f"- Tirones >33,33 / >50 / >100 ms: {frame.get('hitches_over_33_33_ms', 0)} / {frame.get('hitches_over_50_ms', 0)} / {frame.get('hitches_over_100_ms', 0)}",
        "",
        "## Integridad",
        "",
    ]
    warnings = result["integrity"]["warnings"]
    if warnings:
        lines.extend(f"- ⚠ {warning}" for warning in warnings)
    else:
        lines.append("- Sesión cerrada limpiamente y sin muestras descartadas.")

    lines.extend(["", "## Resumen estadístico", "", "| Métrica | Unidad | Media | p95 | p99 | Máximo |", "|---|---:|---:|---:|---:|---:|"])
    for name, unit, values in stat_rows(result):
        lines.append(
            f"| {name} | {unit} | {fmt(values.get('mean'))} | {fmt(values.get('p95'))} | {fmt(values.get('p99'))} | {fmt(values.get('max'))} |"
        )

    lines.extend(["", "## Comparación", ""])
    if baseline_result and comparison:
        lines.append(f"Baseline compatible: `{baseline_result['session_name']}`")
        lines.extend(["", "| Métrica | Baseline | Actual | Variación | Estado |", "|---|---:|---:|---:|---|"])
        labels = {"regression": "regresión", "improvement": "mejora", "stable": "estable"}
        for row in comparison:
            lines.append(
                f"| {row['metric']} | {fmt(row['baseline'])} {row['unit']} | {fmt(row['current'])} {row['unit']} | {fmt(row['delta_percent'], suffix='%')} | {labels[row['status']]} |"
            )
    else:
        lines.append("No se encontró una sesión anterior compatible para comparar.")
    lines.append("")
    lines.append("La memoria de vídeo se informa solo si Unity expone un contador compatible (`Video Used Memory`, `Video Memory Bytes` o `Gfx Used Memory`); no equivale a la capacidad total de VRAM de la tarjeta.")
    return "\n".join(lines) + "\n"


def create_html(
    result: dict[str, Any], analyzed_frames: Sequence[Frame], baseline_result: dict[str, Any] | None, comparison: list[dict[str, Any]], forced_baseline_differences: list[str]
) -> str:
    metadata = result["metadata"]
    frame = result["frame_time_ms"]
    warnings = result["integrity"]["warnings"]
    warning_html = (
        "<ul>" + "".join(f"<li>{html.escape(item)}</li>" for item in warnings) + "</ul>"
        if warnings
        else "<p class='ok'>Sesión cerrada limpiamente y sin muestras descartadas.</p>"
    )

    cards = [
        ("FPS medios", fmt(frame.get("average_fps"))),
        ("1% low", fmt(frame.get("one_percent_low_fps"), suffix=" FPS")),
        ("Frame p95", fmt(frame.get("p95"), suffix=" ms")),
        ("Frame p99", fmt(frame.get("p99"), suffix=" ms")),
        ("Picos >50 ms", fmt(frame.get("hitches_over_50_ms"))),
        ("CPU principal p95", fmt(result["cpu_gpu_ms"]["main_thread"].get("p95"), suffix=" ms")),
        ("GPU p95", fmt(result["cpu_gpu_ms"]["gpu_frame"].get("p95"), suffix=" ms")),
        ("Memoria Unity pico", fmt(result["memory"]["total_used_mib"].get("max"), suffix=" MiB")),
    ]
    cards_html = "".join(
        f"<div class='card'><span>{html.escape(label)}</span><strong>{html.escape(value)}</strong></div>"
        for label, value in cards
    )

    scenes = []
    for scene in result["per_scene"]:
        values = scene["frame_time_ms"]
        scenes.append(
            "<tr>"
            f"<td>{scene['scene_build_index']}</td><td>{html.escape(scene['scene_name'])}</td>"
            f"<td>{fmt(scene['duration_seconds'])}</td><td>{fmt(values.get('count'))}</td>"
            f"<td>{fmt(values.get('average_fps'))}</td><td>{fmt(values.get('one_percent_low_fps'))}</td>"
            f"<td>{fmt(values.get('p95'))}</td><td>{fmt(values.get('max'))}</td>"
            f"<td>{fmt(values.get('hitches_over_50_ms'))}</td></tr>"
        )
    scenes_html = (
        "<table><thead><tr><th>Índice</th><th>Escena activa</th><th>Segundos</th><th>Frames</th>"
        "<th>FPS medios</th><th>1% low</th><th>p95 ms</th><th>Máx ms</th><th>&gt;50 ms</th>"
        "</tr></thead><tbody>" + "".join(scenes) + "</tbody></table>"
    )

    if baseline_result and comparison:
        comparison_rows = []
        for row in comparison:
            comparison_rows.append(
                f"<tr class='{row['status']}'><td>{html.escape(row['metric'])}</td>"
                f"<td>{fmt(row['baseline'])} {html.escape(row['unit'])}</td>"
                f"<td>{fmt(row['current'])} {html.escape(row['unit'])}</td>"
                f"<td>{fmt(row['delta_percent'], suffix='%')}</td><td>{row['status']}</td></tr>"
            )
        difference_note = ""
        if forced_baseline_differences:
            difference_note = "<p class='warning'><strong>Comparación forzada con condiciones distintas:</strong> " + html.escape("; ".join(forced_baseline_differences)) + "</p>"
        comparison_html = (
            f"<p>Baseline: <code>{html.escape(baseline_result['session_name'])}</code></p>"
            + difference_note
            + "<table><thead><tr><th>Métrica</th><th>Baseline</th><th>Actual</th><th>Variación</th><th>Estado</th></tr></thead><tbody>"
            + "".join(comparison_rows)
            + "</tbody></table>"
        )
    else:
        comparison_html = "<p class='muted'>No se encontró una sesión anterior compatible (misma plataforma, CPU, GPU, calidad y resolución).</p>"

    dirty = " · cambios sin commit" if metadata.get("gitDirty") else ""
    return f"""<!doctype html>
<html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Telemetría · {html.escape(result['session_name'])}</title>
<style>
:root{{--bg:#101318;--panel:#171c23;--line:#2a3340;--text:#edf2f7;--muted:#9eabb9;--accent:#68d5b2;--red:#ff7b72;--green:#6fdd8b;--yellow:#e3b341}}
*{{box-sizing:border-box}} body{{margin:0;background:var(--bg);color:var(--text);font:15px/1.5 system-ui,Segoe UI,sans-serif}}
main{{max-width:1240px;margin:auto;padding:34px 24px 70px}} h1{{font-size:30px;margin:0 0 8px}} h2{{margin-top:34px}}
.subtitle,.muted{{color:var(--muted)}} code{{background:#0d1117;padding:2px 6px;border-radius:5px}}
.cards{{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:22px 0}}
.card{{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:14px}} .card span{{display:block;color:var(--muted);font-size:13px}} .card strong{{font-size:23px}}
.panel{{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:18px;overflow:auto}}
table{{width:100%;border-collapse:collapse;min-width:720px}} th,td{{padding:9px 10px;border-bottom:1px solid var(--line);text-align:right}} th:first-child,td:first-child{{text-align:left}} th{{color:var(--muted);font-weight:600}}
.ok{{color:var(--green)}} .warning,li{{color:var(--yellow)}} tr.regression td:last-child{{color:var(--red)}} tr.improvement td:last-child{{color:var(--green)}}
svg{{width:100%;min-width:680px;background:#0d1117;border-radius:8px}} .frame-line{{fill:none;stroke:var(--accent);stroke-width:1.5}} .guide{{stroke:#465363;stroke-dasharray:5 5}} .axis{{fill:var(--muted);font-size:12px}}
</style></head><body><main>
<h1>Informe de telemetría de build</h1>
<p class="subtitle"><code>{html.escape(result['session_name'])}</code></p>
<p>Commit <code>{html.escape(str(metadata.get('gitCommit','unknown')))}</code> · rama <code>{html.escape(str(metadata.get('gitBranch','unknown')))}</code>{dirty}<br>
{html.escape(str(metadata.get('qualityName','N/D')))} · {metadata.get('width','N/D')}×{metadata.get('height','N/D')} · Unity {html.escape(str(metadata.get('unityVersion','N/D')))}<br>
{html.escape(str(metadata.get('processorType','N/D')))} · {html.escape(str(metadata.get('graphicsDeviceName','N/D')))}</p>
<div class="cards">{cards_html}</div>
<p class="muted">Cifras principales sobre {fmt(result['capture']['analyzed_frame_count'])} frames / {fmt(result['capture']['analyzed_duration_seconds'])} s. Se excluyeron {fmt(result['capture']['warmup_excluded_seconds'])} s iniciales. La sesión completa también queda en summary.json.</p>
<h2>Tiempo de frame</h2><div class="panel">{svg_frame_chart(analyzed_frames)}</div>
<h2>Integridad de captura</h2><div class="panel">{warning_html}</div>
<h2>CPU, GPU, memoria y render</h2><div class="panel">{html_stats_table(stat_rows(result))}</div>
<h2>Por escena activa</h2><div class="panel">{scenes_html}</div>
<h2>Comparación</h2><div class="panel">{comparison_html}</div>
<p class="muted">Todos los agregados de este documento se calcularon después de cerrar la build. La memoria de vídeo solo aparece si Unity expuso un contador compatible y no representa la capacidad total de VRAM.</p>
</main></body></html>"""


def unique_report_directory(root: Path, session: Session) -> Path:
    stamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
    session_id = str(session.metadata.get("sessionId", session.name))[:8]
    base = root / f"report_{stamp}_{session_id}"
    candidate = base
    suffix = 2
    while candidate.exists():
        candidate = Path(f"{base}_{suffix:02d}")
        suffix += 1
    candidate.mkdir(parents=True)
    return candidate


def generate_report(
    session: Session,
    output_root: Path,
    warmup_seconds: float,
    baseline: Session | None,
    forced_baseline: bool,
) -> Path:
    result = calculate_session(session, warmup_seconds)
    analyzed_frames, _ = analysis_window(session.frames, warmup_seconds)
    baseline_result = calculate_session(baseline, warmup_seconds) if baseline else None
    comparison = compare_results(result, baseline_result) if baseline_result else []
    differences = (
        compatibility_differences(result["metadata"], baseline_result["metadata"])
        if baseline_result and forced_baseline
        else []
    )
    if baseline_result:
        result["comparison"] = {
            "baseline_session": baseline_result["session_name"],
            "forced": forced_baseline,
            "compatibility_differences": differences,
            "metrics": comparison,
        }
    else:
        result["comparison"] = None

    report_directory = unique_report_directory(output_root, session)
    with (report_directory / "summary.json").open("w", encoding="utf-8") as handle:
        json.dump(result, handle, ensure_ascii=False, indent=2)
    write_summary_csv(report_directory / "summary.csv", result)
    write_scene_csv(report_directory / "scenes.csv", result)
    (report_directory / "report.md").write_text(
        create_markdown(result, baseline_result, comparison), encoding="utf-8"
    )
    (report_directory / "report.html").write_text(
        create_html(result, analyzed_frames, baseline_result, comparison, differences),
        encoding="utf-8",
    )
    return report_directory


def write_synthetic_session(root: Path, name: str, mean_ms: float) -> Path:
    session = root / name
    session.mkdir(parents=True)
    metadata = {
        "schemaVersion": 1,
        "sessionId": name[-8:],
        "utcStarted": datetime.now(timezone.utc).isoformat(),
        "gitCommit": "selftest123",
        "gitBranch": "self-test",
        "gitDirty": False,
        "unityVersion": "6000.0.test",
        "platform": "WindowsPlayer",
        "processorType": "Synthetic CPU",
        "graphicsDeviceName": "Synthetic GPU",
        "qualityName": "PC",
        "width": 1920,
        "height": 1080,
    }
    (session / "session.json").write_text(json.dumps(metadata), encoding="utf-8")
    with (session / "frames.bin").open("wb") as handle:
        handle.write(HEADER.pack(MAGIC, FORMAT_VERSION, FRAME_STRUCT.size))
        realtime = 0.0
        flags = (1 << 15) - 1
        for index in range(600):
            frame_ms = 55.0 if index and index % 120 == 0 else mean_ms + (index % 7 - 3) * 0.08
            realtime += frame_ms / 1000.0
            values = (
                index,
                realtime,
                frame_ms / 1000.0,
                1.0,
                9_000_000,
                5_000_000,
                8_000_000,
                1024 * (index % 5),
                4_000_000_000,
                800_000_000,
                1_000_000_000,
                600_000_000,
                250_000_000,
                50_000_000,
                900,
                700,
                80,
                450_000,
                700_000,
                index // 250,
                index // 400,
                0,
                1,
                0,
                1920,
                1080,
                -1,
                0,
                flags,
            )
            handle.write(FRAME_STRUCT.pack(*values))
    events = {
        "eventType": "scene_loaded",
        "name": "SyntheticScene",
        "value": "Single",
        "scenePath": "Assets/SyntheticScene.unity",
        "sceneBuildIndex": 1,
        "frameIndex": 0,
        "realtimeSinceStartupSeconds": 0.0,
        "utc": datetime.now(timezone.utc).isoformat(),
    }
    (session / "events.jsonl").write_text(json.dumps(events) + "\n", encoding="utf-8")
    (session / "session_end.json").write_text(
        json.dumps(
            {
                "cleanShutdown": True,
                "framesWritten": 600,
                "droppedFrameSamples": 0,
                "droppedEvents": 0,
                "writerError": "",
            }
        ),
        encoding="utf-8",
    )
    (session / "complete.flag").write_text("complete\n", encoding="utf-8")
    return session


def run_self_test() -> None:
    assert FRAME_STRUCT.size == 184, FRAME_STRUCT.size
    with tempfile.TemporaryDirectory(prefix="fatto_telemetry_test_") as temporary:
        root = Path(temporary)
        raw = root / "raw"
        reports = root / "reports"
        raw.mkdir()
        baseline_path = write_synthetic_session(raw, "session_2026-01-01_baseline", 16.0)
        current_path = write_synthetic_session(raw, "session_2026-01-02_current1", 18.0)
        baseline = load_session(baseline_path)
        current = load_session(current_path)
        report = generate_report(current, reports, 1.0, baseline, False)
        summary = load_json(report / "summary.json", {})
        assert summary["frame_time_ms"]["count"] > 400
        assert summary["frame_time_ms"]["p95"] > 17.0
        assert summary["comparison"]["metrics"]
        for filename in ("report.html", "report.md", "summary.json", "summary.csv", "scenes.csv"):
            assert (report / filename).exists(), filename
    print("SELF-TEST OK: lectura binaria, cálculos, comparación e informes.")


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Genera estadísticas posteriores a partir de sesiones crudas de Unity."
    )
    parser.add_argument("--input", type=Path, help="Carpeta que contiene session_*.")
    parser.add_argument("--output", type=Path, help="Carpeta donde crear informes fechados.")
    parser.add_argument("--session", help="Ruta o parte del nombre de la sesión; por defecto, la última.")
    parser.add_argument(
        "--baseline",
        default="auto",
        help="'auto', 'none', ruta o parte del nombre de otra sesión.",
    )
    parser.add_argument("--warmup-seconds", type=float, default=3.0)
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_args(argv or sys.argv[1:])
    if arguments.self_test:
        run_self_test()
        return 0
    if arguments.input is None or arguments.output is None:
        print("[ERROR] --input y --output son obligatorios.", file=sys.stderr)
        return 2
    if arguments.warmup_seconds < 0:
        print("[ERROR] --warmup-seconds no puede ser negativo.", file=sys.stderr)
        return 2

    try:
        session_paths = discover_sessions(arguments.input.resolve())
        current_path = select_session(session_paths, arguments.session)
        current = load_session(current_path)

        baseline: Session | None = None
        forced_baseline = False
        if arguments.baseline.lower() != "none":
            if arguments.baseline.lower() == "auto":
                baseline_path = find_automatic_baseline(
                    session_paths, current_path, current.metadata
                )
            else:
                baseline_path = select_session(session_paths, arguments.baseline)
                forced_baseline = True
            if baseline_path and baseline_path.resolve() != current_path.resolve():
                baseline = load_session(baseline_path)

        report = generate_report(
            current,
            arguments.output.resolve(),
            arguments.warmup_seconds,
            baseline,
            forced_baseline,
        )
    except (ValueError, OSError) as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 1

    print("Informe generado correctamente:")
    print(report.resolve())
    print(f"HTML: {(report / 'report.html').resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
