from pathlib import Path
import importlib.util
import tarfile


ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "Server"
ASSETS = ROOT / "Assets" / "Scripts" / "Networking"


def read(path):
    return path.read_text(encoding="utf-8")


def load_restore_module():
    spec = importlib.util.spec_from_file_location("restore_with_paramiko", SERVER / "restore_with_paramiko.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_unity_uses_public_https_domain():
    auth_manager = read(ASSETS / "AuthManager.cs")
    network_manager = read(ASSETS / "NetworkManager.cs")

    assert 'private string baseUrl = "https://labshieldprotocol.my.id/api";' in auth_manager
    assert 'private string registerUrl = "https://labshieldprotocol.my.id/register.html";' in auth_manager
    assert '[SerializeField] private string serverUrl = "https://labshieldprotocol.my.id/api/submit-score";' in network_manager
    assert "http://2.27.165.46:5000" not in auth_manager
    assert "http://2.27.165.46:5000" not in network_manager


def test_paramiko_defaults_target_home_lab():
    restore_script = read(SERVER / "restore_with_paramiko.py")

    assert 'LABSHIELD_SSH_HOST", "192.168.100.142"' in restore_script
    assert 'LABSHIELD_SSH_USER", "carloserver"' in restore_script
    assert "sudo -S" in restore_script
    assert "cloudflared" in restore_script


def test_helper_scripts_no_longer_default_to_vps_root():
    helper_names = [
        "quick_restore_paramiko.py",
        "configure_smtp_paramiko.py",
        "configure_labshield_nginx_paramiko.py",
        "run_vps_command_paramiko.py",
        "remote_status.py",
        "check_vps_paramiko.py",
        "diagnose_otp_paramiko.py",
        "verify_otp_origin_paramiko.py",
        "inspect_live_database_paramiko.py",
        "seed_students_paramiko.py",
    ]

    for helper_name in helper_names:
        text = read(SERVER / helper_name)
        assert "2.27.165.46" not in text, helper_name
        assert 'LABSHIELD_SSH_HOST", "192.168.100.142"' in text or 'HOST = "192.168.100.142"' in text, helper_name
        assert (
            'LABSHIELD_SSH_USER", "carloserver"' in text
            or 'USER = "carloserver"' in text
            or 'user = os.environ.get("LABSHIELD_SSH_USER", "carloserver")' in text
        ), helper_name


def test_angket_endpoints_exist():
    server_js = read(SERVER / "server.js")
    assert "app.post('/api/angket/submit'" in server_js
    assert "app.get('/api/angket/status'" in server_js
    assert "angket_responses.json" in server_js


def test_angket_teacher_endpoints_exist():
    server_js = read(SERVER / "server.js")
    assert "app.get('/api/angket/responses'" in server_js
    assert "app.post('/api/export-angket.csv'" in server_js
    assert "buildAngketCsv" in server_js

def test_angket_page_exists():
    assert (SERVER / "public" / "angket.html").exists()


def test_dashboard_has_angket_link():
    dashboard = read(SERVER / "public" / "student-dashboard.html")
    assert "angket.html" in dashboard


def test_angket_dashboard_and_export_do_not_show_averages():
    dashboard = read(SERVER / "public" / "dashboard.html")
    server_js = read(SERVER / "server.js")

    assert "Rata-rata Keseluruhan" not in dashboard
    assert "Rata-rata</th>" not in dashboard
    assert "Rata-rata" not in server_js.split("const buildAngketCsv", 1)[1].split("};", 1)[0]


def test_deploy_archive_contains_each_file_once(tmp_path):
    server_dir = tmp_path / "Server"
    nested_dir = server_dir / "public" / "assets" / "materials"
    nested_dir.mkdir(parents=True)
    (server_dir / "server.js").write_text("console.log('ok');", encoding="utf-8")
    (nested_dir / "guide.pdf").write_text("pdf", encoding="utf-8")

    restore = load_restore_module()
    restore.EXCLUDE_VIDEOS = False
    archive_path = restore.make_archive(server_dir)

    with tarfile.open(archive_path, "r:gz") as archive:
        names = [member.name for member in archive.getmembers()]

    assert names.count("Server/server.js") == 1
    assert names.count("Server/public/assets/materials/guide.pdf") == 1
