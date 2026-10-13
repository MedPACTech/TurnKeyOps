import csv
import hashlib
import importlib.util
import json
import tempfile
import unittest
from unittest.mock import patch
from decimal import Decimal
from pathlib import Path

spec = importlib.util.spec_from_file_location("normalize_exports", Path(__file__).with_name("normalize_exports.py"))
normalizer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(normalizer)

class NormalizeExportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.directory = Path(self.tmp.name)
        self.write("accounts.csv", [["externalAccount", "account"], ["10100", "1000"], ["30000", "3000"]])
        self.write("gl.csv", [["externalAccount", "debit", "credit"], ["10100", "999999999999.99", "0"], ["30000", "0", "999999999999.99"]])
        (self.directory / "controls.json").write_text('{"ar":0,"ap":0,"jobCosts":0,"commitments":0,"inventory":0,"cash":{"operating":999999999999.99}}')
        self.template = {"request": {"expectedVersion": 'W/"version1"', "expectedSupplyVersion": "supply1", "newAccounts": []}}
    def tearDown(self):
        self.tmp.cleanup()
    def write(self, name, rows):
        with (self.directory / name).open("w", newline="") as stream:
            csv.writer(stream).writerows(rows)
    def normalize(self, columns=None):
        return normalizer.normalize(self.directory, self.template, "Acumatica", "export-1", "2026-09-30", "Reviewed source controls", columns)
    def test_construction_source_manifest_and_controls(self):
        sections = {key: [] for key in ("constructionContracts", "constructionHistory", "constructionBudgets", "constructionForecasts")}
        (self.directory / "construction.json").write_text(json.dumps(sections))
        with self.assertRaises(ValueError): self.normalize()
        controls = json.loads((self.directory / "controls.json").read_text(), parse_float=Decimal)
        controls.update({key: 0 for key in ("contractValue", "constructionBilled", "retainage", "earnedRevenue", "constructionBudget")})
        (self.directory / "controls.json").write_text(normalizer.exact_json(controls))
        request, manifest = self.normalize()
        self.assertIn("constructionHistory", request)
        self.assertTrue(any(item["file"] == "construction.json" for item in manifest))

    def test_large_decimal_cents_survive_without_binary_floats(self):
        request, _ = self.normalize()
        decoded = json.loads(normalizer.exact_json(request), parse_float=Decimal)
        self.assertEqual(Decimal("999999999999.99"), decoded["trialBalance"][0]["debit"])
        self.assertEqual(Decimal("999999999999.99"), decoded["controls"]["cash"]["operating"])
        self.assertEqual('W/"version1"', decoded["expectedVersion"])
    def test_raw_file_manifest_digest_changes_with_source(self):
        first, manifest = self.normalize()
        self.assertEqual(64, len(first["sourceFileSha256"]))
        self.assertEqual(3, len(manifest))
        self.write("gl.csv", [["externalAccount", "debit", "credit"], ["10100", "1", "0"], ["30000", "0", "1"]])
        second, _ = self.normalize()
        self.assertNotEqual(first["sourceFileSha256"], second["sourceFileSha256"])
    def test_arbitrary_erp_headers_require_explicit_mapping(self):
        self.write("gl.csv", [["AccountCD", "DebitBalance", "CreditBalance"], ["10100", "50.00", "0"], ["30000", "0", "50.00"]])
        with self.assertRaises(ValueError): self.normalize()
        request, _ = self.normalize({"trialBalance": {"externalAccount": "AccountCD", "debit": "DebitBalance", "credit": "CreditBalance"}})
        self.assertEqual(Decimal("50.00"), request["trialBalance"][0]["debit"])
    def test_unknown_columns_are_never_silently_dropped(self):
        self.write("gl.csv", [["externalAccount", "debit", "credit", "ForeignCurrency"], ["10100", "1", "0", "EUR"]])
        with self.assertRaises(ValueError): self.normalize()
        request, _ = self.normalize({"ignoreColumns": {"trialBalance": ["ForeignCurrency"]}})
        self.assertEqual(1, len(request["trialBalance"]))
    def test_invalid_fractional_cent_source_is_rejected(self):
        self.write("gl.csv", [["externalAccount", "debit", "credit"], ["10100", "1.001", "0"]])
        with self.assertRaisesRegex(ValueError, "two decimal places"): self.normalize()
    def test_invalid_dates_canonical_ids_and_nonfinite_values_are_rejected(self):
        for value, kind in [("NaN", "money"), ("Infinity", "quantity"), ("2026-02-30", "date"), ("not-an-id", "guid"), ("0" * 32, "guid")]:
            with self.assertRaises((ValueError, ArithmeticError)): normalizer.parse_value(value, kind)
    def test_parsed_export_and_manifest_hash_use_the_same_immutable_read(self):
        original = Path.read_bytes
        first = b"externalAccount,debit,credit\n10100,10,0\n30000,0,10\n"
        changed = b"externalAccount,debit,credit\n10100,20,0\n30000,0,20\n"
        reads = []
        def read_bytes(path):
            if path.name == "gl.csv":
                reads.append(path.name)
                return first if len(reads) == 1 else changed
            return original(path)
        with patch.object(Path, "read_bytes", read_bytes):
            request, manifest = self.normalize()
        self.assertEqual(["gl.csv"], reads)
        self.assertEqual(Decimal("10"), request["trialBalance"][0]["debit"])
        self.assertEqual(hashlib.sha256(first).hexdigest(), next(row for row in manifest if row["file"] == "gl.csv")["sha256"])
    def test_source_strings_are_json_escaped_without_execution(self):
        value = {"id": "$(touch /tmp/no) \"\\\n", "amount": Decimal("0.10")}
        self.assertEqual(value, json.loads(normalizer.exact_json(value), parse_float=Decimal))

if __name__ == "__main__":
    unittest.main()
