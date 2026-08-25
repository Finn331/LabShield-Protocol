from pathlib import Path
import unittest


COMPOSE = Path(__file__).resolve().parent / "docker-compose.yml"


class ComposeConfigTests(unittest.TestCase):
    def test_compose_loads_env_file_and_forwards_smtp_settings(self):
        compose = COMPOSE.read_text(encoding="utf-8")

        self.assertIn("env_file:", compose)
        self.assertIn("- .env", compose)
        for key in [
            "SMTP_HOST",
            "SMTP_PORT",
            "SMTP_SECURE",
            "SMTP_USER",
            "SMTP_PASS",
            "SMTP_FROM",
            "OTP_SECRET",
        ]:
            self.assertIn(f"- {key}=${{{key}}}", compose)


if __name__ == "__main__":
    unittest.main()
