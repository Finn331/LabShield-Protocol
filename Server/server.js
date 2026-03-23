const express = require('express');
const bodyParser = require('body-parser');
const fs = require('fs');
const path = require('path');
const app = express();

// Use PORT from environment variable or default to 3000
const PORT = process.env.PORT || 3000;

// Middleware
app.use(bodyParser.json());
app.use(express.static('public')); // Serve static files from 'public' folder

// Database Files
const DATA_DIR = process.env.DATA_DIR || './';
const SCORES_DB = path.join(DATA_DIR, 'student_scores.json');
const USERS_DB = path.join(DATA_DIR, 'users.json');

// Ensure data directory exists
if (!fs.existsSync(DATA_DIR) && DATA_DIR !== './') {
    fs.mkdirSync(DATA_DIR, { recursive: true });
}

// Helper to read/write JSON
const readJSON = (file) => {
    if (!fs.existsSync(file)) return [];
    try {
        return JSON.parse(fs.readFileSync(file));
    } catch (e) {
        return [];
    }
};

const writeJSON = (file, data) => {
    fs.writeFileSync(file, JSON.stringify(data, null, 2));
};

const clamp01to100 = (value) => {
    if (!Number.isFinite(value)) return 0;
    if (value < 0) return 0;
    if (value > 100) return 100;
    return Math.round(value);
};

const computeStandardScore = (apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong) => {
    const totalCorrect = apdTotalCorrect + quizTotalCorrect;
    const totalWrong = apdTotalWrong + quizTotalWrong;
    const totalAnswered = totalCorrect + totalWrong;
    return totalAnswered > 0 ? Math.round((totalCorrect / totalAnswered) * 100) : 0;
};

// Rubrik K3:
// - Akurasi APD 60%
// - Akurasi kuis 40%
// - Penalti 5 poin untuk tiap APD salah (maks 20 poin)
const computeK3Score = (apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong) => {
    const apdAnswered = apdTotalCorrect + apdTotalWrong;
    const quizAnswered = quizTotalCorrect + quizTotalWrong;

    const apdAccuracy = apdAnswered > 0 ? (apdTotalCorrect / apdAnswered) * 100 : 0;
    const quizAccuracy = quizAnswered > 0 ? (quizTotalCorrect / quizAnswered) * 100 : 0;

    const weightedScore = (apdAccuracy * 0.6) + (quizAccuracy * 0.4);
    const apdPenalty = Math.min(20, apdTotalWrong * 5);
    return clamp01to100(weightedScore - apdPenalty);
};

const normalizeScoreRow = (row) => {
    const apdTotalCorrect = Number(row.apdTotalCorrect || 0);
    const apdTotalWrong = Number(row.apdTotalWrong || 0);
    const quizTotalCorrect = Number(row.quizTotalCorrect || 0);
    const quizTotalWrong = Number(row.quizTotalWrong || 0);
    const apdTimeTakenSeconds = Number(row.apdTimeTakenSeconds || 0);

    const finalScoreStandard = computeStandardScore(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);
    const finalScoreK3 = computeK3Score(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);

    return {
        ...row,
        apdTotalCorrect,
        apdTotalWrong,
        apdTimeTakenSeconds,
        quizTotalCorrect,
        quizTotalWrong,
        finalScore: finalScoreStandard, // backward compatibility
        finalScoreStandard,
        finalScoreK3
    };
};

const normalizeAttemptNumbers = (scores) => {
    if (!Array.isArray(scores)) return [];

    const sortable = scores.map((row, index) => ({ ...row, __index: index }));
    sortable.sort((a, b) => {
        const ta = Date.parse(a.timestamp || 0);
        const tb = Date.parse(b.timestamp || 0);
        if (Number.isNaN(ta) && Number.isNaN(tb)) return a.__index - b.__index;
        if (Number.isNaN(ta)) return -1;
        if (Number.isNaN(tb)) return 1;
        if (ta === tb) return a.__index - b.__index;
        return ta - tb;
    });

    const counterByStudent = new Map();
    for (const row of sortable) {
        const key = String(row.studentName || '').trim().toLowerCase();
        if (!key) continue;
        const next = (counterByStudent.get(key) || 0) + 1;
        counterByStudent.set(key, next);
        row.attemptNumber = next;
    }

    sortable.sort((a, b) => a.__index - b.__index);
    return sortable.map(({ __index, ...row }) => row);
};

const getNextAttemptNumber = (scores, studentName) => {
    const key = String(studentName || '').trim().toLowerCase();
    if (!key) return 1;

    let maxAttempt = 0;
    for (const row of scores) {
        if (String(row.studentName || '').trim().toLowerCase() !== key) continue;
        const n = Number(row.attemptNumber || 0);
        if (Number.isFinite(n) && n > maxAttempt) maxAttempt = n;
    }
    return maxAttempt + 1;
};

// Seed Admin Helper
const seedAdmin = () => {
    const users = readJSON(USERS_DB);
    const adminExists = users.some(u => u.username === 'admin');
    if (!adminExists) {
        console.log('Seeding default admin account...');
        users.push({ username: 'admin', password: 'aloganteng03.', role: 'teacher' });
        writeJSON(USERS_DB, users);
    }
};

// --- RATE LIMITING ---
const rateLimit = new Map();
const WINDOW_Ms = 5 * 60 * 1000; // 5 minutes
const MAX_ATTEMPTS = 5;

const loginLimiter = (req, res, next) => {
    const ip = req.ip;
    const now = Date.now();

    if (!rateLimit.has(ip)) {
        rateLimit.set(ip, { count: 1, startTime: now });
        return next();
    }

    const record = rateLimit.get(ip);

    // Reset if window passed
    if (now - record.startTime > WINDOW_Ms) {
        record.count = 1;
        record.startTime = now;
        return next();
    }

    // Check limit
    if (record.count >= MAX_ATTEMPTS) {
        return res.status(429).json({
            error: `Too many attempts. Please try again in ${Math.ceil((WINDOW_Ms - (now - record.startTime)) / 60000)} minutes.`
        });
    }

    record.count++;
    next();
};

// --- AUTHENTICATION ROUTES ---

// Register (Student Only - Public)
app.post('/api/register', loginLimiter, (req, res) => {
    const { username, password } = req.body;
    if (!username || !password) return res.status(400).json({ error: 'Username and password required' });

    const users = readJSON(USERS_DB);
    if (users.find(u => u.username === username)) {
        return res.status(400).json({ error: 'Username already exists' });
    }

    // In a real app, HASH the password!
    users.push({ username, password, role: 'student' });
    writeJSON(USERS_DB, users);

    console.log(`User registered: ${username}`);
    res.json({ success: true, message: 'Registration successful' });
});

// Create Teacher (Protected - Teacher Only)
app.post('/api/create-teacher', (req, res) => {
    const { requesterUsername, requesterPassword, newUsername, newPassword } = req.body;

    // Auth Check (Very simple implementation, normally use Tokens)
    const users = readJSON(USERS_DB);
    const adminUser = users.find(u => u.username === requesterUsername && u.password === requesterPassword);

    if (!adminUser || adminUser.role !== 'teacher') {
        return res.status(403).json({ error: 'Unauthorized: Only teachers can create new teachers.' });
    }

    if (users.find(u => u.username === newUsername)) {
        return res.status(400).json({ error: 'Username already exists' });
    }

    users.push({ username: newUsername, password: newPassword, role: 'teacher' });
    writeJSON(USERS_DB, users);

    console.log(`New teacher created by ${requesterUsername}: ${newUsername}`);
    res.json({ success: true, message: 'Teacher created successfully' });
});

// Login (Student & Teacher)
app.post('/api/login', loginLimiter, (req, res) => {
    const { username, password } = req.body;
    const users = readJSON(USERS_DB);

    const user = users.find(u => u.username === username);

    if (!user) {
        return res.status(404).json({ error: 'User not found' });
    }

    if (user.password !== password) {
        return res.status(401).json({ error: 'Incorrect password' });
    }

    res.json({ success: true, role: user.role, username: user.username });
});

// --- SCORE ROUTES ---

// Submit Score (Called by Unity - New Rich Payload)
// Expected payload: { studentName, attemptNumber, apdTotalCorrect, apdTotalWrong, apdTimeTakenSeconds, quizTotalCorrect, quizTotalWrong, questionTimes: [{questionID, timeTaken, isCorrect}] }
app.post('/api/submit-score', (req, res) => {
    const scoreData = req.body || {};
    const studentName = String(scoreData.studentName || '').trim();

    if (!studentName) {
        return res.status(400).json({ error: 'studentName is required' });
    }

    const apdTotalCorrect = Number(scoreData.apdTotalCorrect || 0);
    const apdTotalWrong = Number(scoreData.apdTotalWrong || 0);
    const quizTotalCorrect = Number(scoreData.quizTotalCorrect || 0);
    const quizTotalWrong = Number(scoreData.quizTotalWrong || 0);
    const finalScoreStandard = computeStandardScore(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);
    const finalScoreK3 = computeK3Score(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);

    console.log('Received Score:', JSON.stringify(scoreData).substring(0, 300));

    let scores = normalizeAttemptNumbers(readJSON(SCORES_DB).map(normalizeScoreRow));
    const attemptNumber = getNextAttemptNumber(scores, studentName);
    scores.push({
        ...scoreData,
        studentName,
        attemptNumber,
        apdTotalCorrect,
        apdTotalWrong,
        apdTimeTakenSeconds: Number(scoreData.apdTimeTakenSeconds || 0),
        quizTotalCorrect,
        quizTotalWrong,
        questionTimes: Array.isArray(scoreData.questionTimes) ? scoreData.questionTimes : [],
        finalScore: finalScoreStandard,
        finalScoreStandard,
        finalScoreK3,
        timestamp: new Date().toISOString()
    });
    scores = normalizeAttemptNumbers(scores.map(normalizeScoreRow));
    writeJSON(SCORES_DB, scores);

    res.json({ message: 'Score saved', finalScore: finalScoreStandard, finalScoreStandard, finalScoreK3, attemptNumber });
});

// Get All Scores (Called by Teacher Dashboard)
app.get('/api/scores', (req, res) => {
    const scores = normalizeAttemptNumbers(readJSON(SCORES_DB).map(normalizeScoreRow));
    writeJSON(SCORES_DB, scores);
    res.json(scores);
});

// Get Scores for a specific student (Called by Student Dashboard)
app.get('/api/student-scores/:username', (req, res) => {
    const username = String(req.params.username || '').trim().toLowerCase();
    const scores = normalizeAttemptNumbers(readJSON(SCORES_DB).map(normalizeScoreRow));
    writeJSON(SCORES_DB, scores);
    const studentScores = scores.filter(s => String(s.studentName || '').trim().toLowerCase() === username);
    res.json(studentScores);
});

// --- USER MANAGEMENT ROUTES ---

// Get All Students (Protected - Teacher Only)
app.get('/api/students', (req, res) => {
    // In a real app, use headers/tokens. Here we rely on the dashboard logic to only call this if logged in.
    // For better security, we could pass credentials in query params or headers, 
    // but for now we follow the existing pattern of trust for GET, or strictly we should require auth.
    // Given the simplicity, we'll just return the list but filter out passwords.

    const users = readJSON(USERS_DB);
    const students = users
        .filter(u => u.role === 'student')
        .map(u => ({ username: u.username })); // Don't send passwords
    res.json(students);
});

// Get All Teachers (Protected - Teacher Only)
app.get('/api/teachers', (req, res) => {
    const users = readJSON(USERS_DB);
    const teachers = users
        .filter(u => u.role === 'teacher')
        .map(u => ({ username: u.username }));
    res.json(teachers);
});

// Delete User (Protected - Teacher Only)
// Changed to POST to avoid issues with DELETE requests containing bodies in some browsers/proxies
app.post('/api/delete-user', (req, res) => {
    const { targetUsername, requesterUsername, requesterPassword } = req.body;

    const users = readJSON(USERS_DB);
    const requester = users.find(u => u.username === requesterUsername && u.password === requesterPassword);

    if (!requester || requester.role !== 'teacher') {
        return res.status(403).json({ error: 'Unauthorized: Only teachers can delete users.' });
    }

    const initialLength = users.length;
    const newUsers = users.filter(u => u.username !== targetUsername);

    if (newUsers.length === initialLength) {
        return res.status(404).json({ error: 'User not found' });
    }

    writeJSON(USERS_DB, newUsers);
    console.log(`User ${targetUsername} deleted by ${requesterUsername}`);
    res.json({ success: true, message: 'User deleted successfully' });
});

// Batch Delete Users
app.post('/api/delete-users', (req, res) => {
    const { targetUsernames, requesterUsername, requesterPassword } = req.body;

    const users = readJSON(USERS_DB);
    const requester = users.find(u => u.username === requesterUsername && u.password === requesterPassword);

    if (!requester || requester.role !== 'teacher') {
        return res.status(403).json({ success: false, error: 'Unauthorized: Teacher access required' });
    }

    if (!targetUsernames || !Array.isArray(targetUsernames) || targetUsernames.length === 0) {
        return res.status(400).json({ success: false, error: 'No users specified for deletion' });
    }

    let deletedCount = 0;
    let failedCount = 0;
    const newUsers = users.filter(user => {
        if (targetUsernames.includes(user.username)) {
            // Protect 'admin' and self-deletion
            if (user.username === 'admin' || user.username === requesterUsername) {
                failedCount++;
                return true; // Keep protected user
            }
            deletedCount++;
            return false; // Remove user
        }
        return true; // Keep unselected user
    });

    if (deletedCount > 0) {
        writeJSON(USERS_DB, newUsers);
        // Also remove scores for deleted students
        const scores = readJSON(SCORES_DB);
        const newScores = scores.filter(s => !targetUsernames.includes(s.studentName));
        writeJSON(SCORES_DB, newScores);
    }

    res.json({ success: true, message: `Deleted ${deletedCount} users. ${failedCount > 0 ? `Failed to delete ${failedCount} protected users.` : ''}` });
});

// Start Server
app.listen(PORT, '0.0.0.0', () => {
    console.log(`Server running on port ${PORT}`);
    // Create DB files if not exist
    if (!fs.existsSync(USERS_DB)) writeJSON(USERS_DB, []);
    if (!fs.existsSync(SCORES_DB)) writeJSON(SCORES_DB, []);
    // Create public folder for dashboard
    if (!fs.existsSync('public')) fs.mkdirSync('public', { recursive: true });

    // Seed Admin
    seedAdmin();
});
