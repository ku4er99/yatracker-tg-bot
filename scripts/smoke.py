"""Validate production API credentials without printing secrets or issue contents."""

import json
import os
import sys
import urllib.error
import urllib.request


def request_json(label, request):
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        raise SystemExit(f"{label}: HTTP {error.code}") from None
    except Exception as error:
        raise SystemExit(f"{label}: {type(error).__name__}") from None


telegram = request_json(
    "Telegram getMe",
    urllib.request.Request(
        f"https://api.telegram.org/bot{os.environ['TELEGRAM_BOT_TOKEN']}/getMe"
    ),
)
username = telegram.get("result", {}).get("username", "")
if not telegram.get("ok") or username.lower() != "yatrackertasksbot":
    raise SystemExit("Telegram getMe: token belongs to a different bot")

org_type = os.environ["TRACKER_ORG_TYPE"]
if org_type not in ("cloud", "360"):
    raise SystemExit("TRACKER_ORG_TYPE must be cloud or 360")
headers = {
    "Authorization": f"OAuth {os.environ['TRACKER_OAUTH_TOKEN']}",
    "Content-Type": "application/json",
    ("X-Cloud-Org-ID" if org_type == "cloud" else "X-Org-ID"): os.environ[
        "TRACKER_ORG_ID"
    ],
}
query = {
    "query": 'Assignee: me() Resolution: empty() "Sort by": Updated DESC'
}
issues = request_json(
    "Tracker search",
    urllib.request.Request(
        "https://api.tracker.yandex.net/v3/issues/_search?perPage=1&page=1",
        data=json.dumps(query).encode(),
        headers=headers,
        method="POST",
    ),
)
if not isinstance(issues, list):
    raise SystemExit("Tracker search: unexpected response type")

print(f"Telegram getMe: @{username}; Tracker search: OK")
