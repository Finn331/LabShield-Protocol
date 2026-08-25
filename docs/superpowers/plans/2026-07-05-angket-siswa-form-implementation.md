# Angket Siswa Form Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create one-time angket form with 20 Likert-scale questions for students, accessible from student dashboard.

**Architecture:** New `Server/public/angket.html` page using 21st-dev UI patterns adapted to vanilla HTML/JS. New backend routes in `server.js` for submit and status check. Data stored in `Server/angket_responses.json`. One-time enforcement on client and server.

**Tech Stack:** Node.js/Express, vanilla HTML/CSS/JS, 21st-dev UI inspiration, style.css

---

### Task 1: Add failing regression test for angket endpoints

**Files:**
- Modify: `Server/test_home_lab_config.py`

- [ ] **Step 1: Add test functions**

Add to `Server/test_home_lab_config.py`:
```python
def test_angket_endpoints_exist():
    server_js = read(SERVER / "server.js")
    assert "app.post('/api/angket/submit'" in server_js
    assert "app.get('/api/angket/status'" in server_js
    assert "angket_responses.json" in server_js

def test_angket_page_exists():
    assert (SERVER / "public" / "angket.html").exists()

def test_dashboard_has_angket_link():
    dashboard = read(SERVER / "public" / "student-dashboard.html")
    assert "angket.html" in dashboard
```

- [ ] **Step 2: Run test to verify it fails**

Run: `py -m pytest Server/test_home_lab_config.py::test_angket_endpoints_exist Server/test_home_lab_config.py::test_angket_page_exists Server/test_home_lab_config.py::test_dashboard_has_angket_link -q`
Expected: FAIL

### Task 2: Create angket.html page

**Files:**
- Create: `Server/public/angket.html`

Create a self-contained HTML page with:
- Login check via URL parameter username
- Fetch `/api/angket/status?username=...` on load
- If filled: show "Sudah mengisi" message
- If not filled: show full form with 20 questions
- Radio buttons (STS/TS/S/SS) per question
- Submit button with client-side validation
- Loading/error states
- Follow existing style.css patterns

### Task 3: Add backend endpoints in server.js

**Files:**
- Modify: `Server/server.js`

Add before `app.listen(...)`:
```javascript
const ANGKET_DB = path.join(DATA_DIR, 'angket_responses.json');
// Ensure file exists
if (!fs.existsSync(ANGKET_DB)) writeJSON(ANGKET_DB, []);

// GET /api/angket/status
app.get('/api/angket/status', (req, res) => {
    const username = String(req.query.username || '').trim().toLowerCase();
    if (!username) return res.status(400).json({ error: 'Username required' });
    const responses = readJSON(ANGKET_DB);
    const filled = responses.some(r => r.username === username);
    res.json({ filled });
});

// POST /api/angket/submit
app.post('/api/angket/submit', (req, res) => {
    const { username, jawaban } = req.body || {};
    if (!username || !Array.isArray(jawaban) || jawaban.length !== 20) {
        return res.status(400).json({ error: 'Data tidak valid. Harus username + 20 jawaban.' });
    }
    if (!jawaban.every(n => [1,2,3,4].includes(n))) {
        return res.status(400).json({ error: 'Nilai jawaban harus 1-4 (STS/TS/S/SS).' });
    }
    const normalizedUser = String(username).trim().toLowerCase();
    const responses = readJSON(ANGKET_DB);
    if (responses.some(r => r.username === normalizedUser)) {
        return res.status(403).json({ error: 'Kamu sudah pernah mengisi angket ini.' });
    }
    responses.push({
        username: normalizedUser,
        jawaban,
        timestamp: new Date().toISOString()
    });
    writeJSON(ANGKET_DB, responses);
    res.json({ success: true, message: 'Angket berhasil disimpan. Terima kasih!' });
});
```

### Task 4: Add angket link to student dashboard

**Files:**
- Modify: `Server/public/student-dashboard.html`

Add after score KPI cards or in the action section: a button "Isi Angket Pembelajaran" that checks `/api/angket/status?username=...` and shows/hides based on status.

### Task 5: Run regression tests

**Files:**
- Test: `Server/test_home_lab_config.py`

Run: `py -m pytest Server/test_home_lab_config.py -q`
Expected: All tests pass including new angket tests.

### Task 6: Deploy to home lab and verify

**Files:**
- Execute: deploy updated files to home lab

Use restore script or manual upload to update server.js, angket.html, student-dashboard.html. Then verify endpoint and page publicly.

### Task 7: Final verification

Check:
- `GET https://labshieldprotocol.my.id/angket.html` → 200
- `GET https://labshieldprotocol.my.id/api/angket/status?username=admin` → `{filled:...}`
- `POST https://labshieldprotocol.my.id/api/angket/submit` with test data → success
- Same submit again → 403
