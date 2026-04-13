"""Configuración de pruebas E2E/API (sobrescribir con variables de entorno)."""
import os

BASE_URL = os.environ.get("QA_BASE_URL", "https://sistema-para-tienda.cowib.es").rstrip("/")
# Origen del SPA (login). Si el front está en otro dominio/puerto, ej.: export QA_UI_BASE_URL=http://localhost:5173
UI_BASE_URL = os.environ.get("QA_UI_BASE_URL", BASE_URL).rstrip("/")
API_PREFIX = f"{BASE_URL}/api/v1"

QA_USER = os.environ.get("QA_USER", "admin")
QA_PASSWORD = os.environ.get("QA_PASSWORD", "admin")

REQUEST_TIMEOUT = float(os.environ.get("QA_TIMEOUT", "30"))
MAX_RESPONSE_TIME_WARN_MS = float(os.environ.get("QA_SLOW_MS", "3000"))

SCREENSHOT_DIR = os.environ.get("QA_SCREENSHOT_DIR", os.path.join(os.path.dirname(__file__), "artifacts", "screenshots"))
