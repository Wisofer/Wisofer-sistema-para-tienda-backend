from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Optional


@dataclass
class TestResult:
    name: str
    ok: bool
    ms: float
    detail: str = ""
    error: str = ""
    recommendation: str = ""
    module: str = ""

    def to_dict(self) -> dict[str, Any]:
        return {
            "name": self.name,
            "ok": self.ok,
            "ms": round(self.ms, 2),
            "detail": self.detail,
            "error": self.error,
            "recommendation": self.recommendation,
            "module": self.module,
        }


@dataclass
class QaContext:
    """IDs creados durante la suite (limpieza opcional)."""

    categoria_id: Optional[int] = None
    producto_id: Optional[int] = None
    cliente_id: Optional[int] = None
    usuario_id: Optional[int] = None
    venta_id: Optional[int] = None
    categoria_reasignar_id: Optional[int] = None
    extras: dict[str, Any] = field(default_factory=dict)
