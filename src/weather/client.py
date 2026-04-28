from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any, Dict, Iterable, List, Optional


class WeatherClientError(RuntimeError):
    pass


@dataclass(frozen=True)
class WeatherResult:
    latitude: float
    longitude: float
    timezone: str
    fetched_at_utc: datetime

    temperature_c: Optional[float] = None
    wind_speed_kph: Optional[float] = None
    wind_direction_deg: Optional[float] = None
    weather_code: Optional[int] = None


@dataclass(frozen=True)
class WeatherResponse:
    raw: Dict[str, Any]
    result: WeatherResult


class WeatherClient:
    """Fetch weather info from a public API.

    Uses Open-Meteo (no API key required): https://open-meteo.com/

    Example:
        client = WeatherClient()
        resp = client.get_current_weather(latitude=35.6895, longitude=139.6917)
        print(resp.result.temperature_c)
    """

    def __init__(
        self,
        base_url: str = "https://api.open-meteo.com/v1/forecast",
        timeout_seconds: float = 10.0,
        user_agent: str = "quant-agent-weather-client",
    ) -> None:
        self._base_url = base_url
        self._timeout_seconds = timeout_seconds
        self._user_agent = user_agent

    def get_current_weather(
        self,
        *,
        latitude: float,
        longitude: float,
        timezone_name: str = "UTC",
        include: Iterable[str] = ("temperature_2m", "wind_speed_10m", "wind_direction_10m", "weather_code"),
    ) -> WeatherResponse:
        """Return current weather for the given coordinate.

        Args:
            latitude: Coordinate latitude.
            longitude: Coordinate longitude.
            timezone_name: Timezone name for returned values.
            include: Which current fields to request.
        """

        current_fields = ",".join(_dedupe_preserve_order(include))
        if not current_fields:
            raise WeatherClientError("include must contain at least one field")

        query = {
            "latitude": str(latitude),
            "longitude": str(longitude),
            "timezone": timezone_name,
            "current": current_fields,
        }
        url = f"{self._base_url}?{urllib.parse.urlencode(query)}"
        payload = self._get_json(url)

        result = WeatherResult(
            latitude=float(payload.get("latitude")),
            longitude=float(payload.get("longitude")),
            timezone=str(payload.get("timezone")),
            fetched_at_utc=datetime.now(timezone.utc),
            temperature_c=_maybe_float(_get_nested(payload, ["current", "temperature_2m"])),
            wind_speed_kph=_maybe_float(_get_nested(payload, ["current", "wind_speed_10m"])),
            wind_direction_deg=_maybe_float(_get_nested(payload, ["current", "wind_direction_10m"])),
            weather_code=_maybe_int(_get_nested(payload, ["current", "weather_code"])),
        )
        return WeatherResponse(raw=payload, result=result)

    def _get_json(self, url: str) -> Dict[str, Any]:
        request = urllib.request.Request(
            url,
            method="GET",
            headers={
                "Accept": "application/json",
                "User-Agent": self._user_agent,
            },
        )

        try:
            with urllib.request.urlopen(request, timeout=self._timeout_seconds) as response:
                charset = response.headers.get_content_charset() or "utf-8"
                raw_text = response.read().decode(charset)
        except urllib.error.HTTPError as exc:
            body = None
            try:
                body = exc.read().decode("utf-8", errors="replace")
            except Exception:
                body = None
            raise WeatherClientError(f"HTTP error {exc.code} fetching weather: {body}") from exc
        except urllib.error.URLError as exc:
            raise WeatherClientError(f"Network error fetching weather: {exc.reason}") from exc
        except TimeoutError as exc:
            raise WeatherClientError("Timeout fetching weather") from exc

        try:
            payload = json.loads(raw_text)
        except json.JSONDecodeError as exc:
            raise WeatherClientError("Invalid JSON returned from weather API") from exc

        if not isinstance(payload, dict):
            raise WeatherClientError("Unexpected weather API response")
        return payload


def _get_nested(data: Dict[str, Any], path: List[str]) -> Any:
    cur: Any = data
    for key in path:
        if not isinstance(cur, dict):
            return None
        cur = cur.get(key)
    return cur


def _maybe_float(value: Any) -> Optional[float]:
    if value is None:
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _maybe_int(value: Any) -> Optional[int]:
    if value is None:
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _dedupe_preserve_order(items: Iterable[str]) -> List[str]:
    seen = set()
    out: List[str] = []
    for item in items:
        if item in seen:
            continue
        seen.add(item)
        out.append(item)
    return out

