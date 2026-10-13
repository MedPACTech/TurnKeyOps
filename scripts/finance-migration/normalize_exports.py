#!/usr/bin/env python3
"""Normalize reviewed CSV exports into a Finance migration preview request. Never posts data.

Run with --help. Source systems may export arbitrary headers: --columns explicitly maps
canonical field names to exported column names for each section. No account/party is inferred.
"""
import argparse
import csv
import hashlib
import io
import json
import sys
import uuid
from datetime import date
from decimal import Decimal, InvalidOperation
from pathlib import Path

SECTIONS = {
    "accounts": ("accounts.csv", {"externalAccount": "str", "account": "str"}, {}),
    "entities": ("entities.csv", {"kind": "str", "externalId": "str", "id": "guid"}, {}),
    "trialBalance": ("gl.csv", {"externalAccount": "str", "debit": "money", "credit": "money"}, {}),
    "openItems": ("open-items.csv", {"externalId": "str", "kind": "str", "externalPartyId": "str", "number": "str", "date": "date", "dueDate": "date", "balance": "money"}, {"externalJobId": "str", "existingInvoiceId": "guid"}),
    "jobCosts": ("job-costs.csv", {"externalId": "str", "externalJobId": "str", "category": "str", "amount": "money"}, {"costCode": "str"}),
    "commitments": ("commitments.csv", {"externalId": "str", "externalJobId": "str", "externalVendorId": "str", "description": "str", "category": "str", "remainingAmount": "money"}, {"supplyOrderId": "guid", "supplyOrderLineId": "guid"}),
    "inventory": ("inventory.csv", {"externalId": "str", "itemId": "str", "locationId": "guid", "quantity": "quantity", "value": "money"}, {}),
}

def parse_value(raw, kind):
    raw = raw.strip()
    if kind == "str":
        if not raw or len(raw) > 1000:
            raise ValueError("requires 1–1000 characters")
        return raw
    if kind == "guid":
        value = uuid.UUID(raw)
        if value.int == 0:
            raise ValueError("requires non-empty canonical UUID")
        return str(value)
    if kind == "date":
        return date.fromisoformat(raw).isoformat()
    try:
        value = Decimal(raw)
    except InvalidOperation as exc:
        raise ValueError("requires a plain decimal number without currency symbols/separators") from exc
    precision = Decimal("0.01") if kind == "money" else Decimal("0.000001")
    if not value.is_finite() or abs(value) > Decimal("1000000000000") or value != value.quantize(precision):
        raise ValueError("requires a finite amount within range, with at most " + ("two" if kind == "money" else "six") + " decimal places")
    return value


def read_section(directory, name, column_maps):
    filename, required, optional = SECTIONS[name]
    path = directory / filename
    if not path.exists():
        if name in ("accounts", "trialBalance"):
            raise ValueError(f"Missing required export {filename}")
        return [], None
    mapping = column_maps.get(name, {})
    if set(mapping) - set(required) - set(optional):
        raise ValueError(f"Unknown canonical mapping field in {name}")
    headers = {key: mapping.get(key, key) for key in required | optional}
    if len(set(headers.values())) != len(headers):
        raise ValueError(f"Duplicate source header mapping in {name}")
    source_bytes = path.read_bytes()
    with io.StringIO(source_bytes.decode("utf-8-sig"), newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames is None or len(reader.fieldnames) != len(set(reader.fieldnames)):
            raise ValueError(f"{filename}: missing or duplicate CSV headers")
        if set(headers[key] for key in required) - set(reader.fieldnames):
            raise ValueError(f"{filename}: required headers do not match the explicit mapping")
        # Unknown source columns can be acknowledged by listing them explicitly in ignoreColumns.
        unknown = set(reader.fieldnames) - set(headers.values()) - set(column_maps.get("ignoreColumns", {}).get(name, []))
        if unknown:
            raise ValueError(f"{filename}: unreviewed columns {sorted(unknown)}; map or explicitly ignore them")
        result = []
        for row_no, row in enumerate(reader, 2):
            if None in row:
                raise ValueError(f"{filename}:{row_no}: excess fields (check quoting and delimiters)")
            if not any((value or "").strip() for value in row.values()):
                continue
            parsed = {}
            for key, kind in (required | optional).items():
                raw = row.get(headers[key]) or ""
                if key in optional and not raw.strip():
                    continue
                try:
                    parsed[key] = parse_value(raw, kind)
                except (ValueError, InvalidOperation) as exc:
                    raise ValueError(f"{filename}:{row_no}:{headers[key]}: {exc}") from exc
            result.append(parsed)
    return result, {"file": filename, "sha256": hashlib.sha256(source_bytes).hexdigest(), "rows": len(result)}


def exact_json(value, level=0):
    """Serialize decimal JSON numbers without binary float rounding or quoted amounts."""
    if isinstance(value, Decimal):
        return format(value, "f")
    if isinstance(value, dict):
        return "{" + ",".join(json.dumps(key) + ":" + exact_json(item, level + 1) for key, item in value.items()) + "}"
    if isinstance(value, list):
        return "[" + ",".join(exact_json(item, level + 1) for item in value) + "]"
    return json.dumps(value, ensure_ascii=False, allow_nan=False)


def normalize(directory, template, source_system, export_id, cutover, reason, columns=None):
    # Template comes from the authenticated tenant endpoint, never a guessed tenant ID.
    request = dict(template.get("request", template))
    if "expectedVersion" not in request or "expectedSupplyVersion" not in request:
        raise ValueError("Template must contain exact Finance and Supply versions from /api/finance/migration/template")
    request.update(id=str(uuid.uuid4()), sourceSystem=source_system, exportId=export_id,
                   cutoverDate=date.fromisoformat(cutover).isoformat(), strategy="OPEN_ITEMS_AND_BALANCES",
                   reason=reason, approvedFingerprint="")
    manifest = []
    for name in SECTIONS:
        request[name], source_file = read_section(directory, name, columns or {})
        if source_file:
            manifest.append(source_file)
    construction_path = directory / "construction.json"
    construction_controls = {"contractValue", "constructionBilled", "retainage", "earnedRevenue", "constructionBudget"}
    if construction_path.exists():
        construction_bytes = construction_path.read_bytes()
        construction = json.loads(construction_bytes.decode("utf-8-sig"), parse_float=Decimal)
        sections = {"constructionContracts", "constructionHistory", "constructionBudgets", "constructionForecasts"}
        if not isinstance(construction, dict) or set(construction) != sections or any(not isinstance(construction[key], list) for key in sections):
            raise ValueError("construction.json requires constructionContracts, constructionHistory, constructionBudgets and constructionForecasts arrays")
        request.update(construction)
        manifest.append({"file": "construction.json", "sha256": hashlib.sha256(construction_bytes).hexdigest()})
    else:
        for key in ("constructionContracts", "constructionHistory", "constructionBudgets", "constructionForecasts"):
            request[key] = []
    controls_path = directory / "controls.json"
    controls_bytes = controls_path.read_bytes()
    request["controls"] = json.loads(controls_bytes.decode("utf-8-sig"), parse_float=Decimal, parse_int=Decimal)
    expected = {"ar", "ap", "jobCosts", "commitments", "inventory", "cash"}
    if set(request["controls"]) != (expected | construction_controls if construction_path.exists() else expected) or not isinstance(request["controls"]["cash"], dict):
        raise ValueError("controls.json requires ar, ap, jobCosts, commitments, inventory and cash object")
    for key, value in request["controls"].items():
        if key == "cash":
            request["controls"][key] = {name: parse_value(str(amount), "money") for name, amount in value.items()}
        else:
            request["controls"][key] = parse_value(str(value), "money")
    manifest.append({"file": "controls.json", "sha256": hashlib.sha256(controls_bytes).hexdigest()})
    if columns:
        manifest.append({"file": "explicit-column-mapping", "sha256": hashlib.sha256(json.dumps(columns, sort_keys=True).encode()).hexdigest()})
    # Digest commits to each original file, filename, row count and explicit field mapping.
    request["sourceFileSha256"] = hashlib.sha256(json.dumps(manifest, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return request, manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="Folder of reviewed UTF-8 CSV exports and controls.json")
    parser.add_argument("--template", required=True, type=Path, help="JSON response from the authenticated migration template endpoint")
    parser.add_argument("--columns", type=Path, help="Explicit canonical-to-source CSV header map JSON; optional ignoreColumns per section")
    parser.add_argument("--source-system", default="Acumatica")
    parser.add_argument("--export-id", required=True)
    parser.add_argument("--cutover", required=True, help="ISO YYYY-MM-DD")
    parser.add_argument("--reason", required=True, help="Source-control reconciliation/reviewer evidence")
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    try:
        template = json.loads(args.template.read_text(), parse_float=Decimal)
        # Standard API envelope may wrap the payload in data.
        if isinstance(template.get("data"), dict):
            template = template["data"]
        columns = json.loads(args.columns.read_text()) if args.columns else None
        request, manifest = normalize(args.directory, template, args.source_system, args.export_id, args.cutover, args.reason, columns)
        args.out.write_text(exact_json(request) + "\n")
        args.out.with_suffix(".manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
        print(f"Wrote preview request {args.out}; no data posted. Review the authenticated dry-run report before approval.")
    except (ValueError, OSError, KeyError, TypeError) as exc:
        parser.error(str(exc))

if __name__ == "__main__":
    main()
