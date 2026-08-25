from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parent
SERVER = ROOT / "server.js"
DASHBOARD = ROOT / "public" / "dashboard.html"


class CsvExportTests(unittest.TestCase):
    def test_server_has_protected_csv_export_endpoint(self):
        source = SERVER.read_text(encoding="utf-8")

        self.assertIn("app.post('/api/export-scores.csv'", source)
        self.assertIn("requesterUsername", source)
        self.assertIn("requesterPassword", source)
        self.assertIn("role !== 'teacher'", source)
        self.assertIn("text/csv; charset=utf-8", source)
        self.assertIn("attachment; filename=\"labshield-scores.csv\"", source)

    def test_dashboard_has_export_csv_button_and_request(self):
        html = DASHBOARD.read_text(encoding="utf-8")

        self.assertIn("Export CSV", html)
        self.assertIn("exportScoresCsv()", html)
        self.assertIn("fetch('/api/export-scores.csv'", html)
        self.assertIn("requesterUsername: currentUser", html)
        self.assertIn("requesterPassword: currentPass", html)


if __name__ == "__main__":
    unittest.main()
