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
const WINDOW_Ms = 15 * 60 * 1000; // 15 minutes
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

// Submit Score (Called by Unity)
app.post('/api/submit-score', (req, res) => {
    const scoreData = req.body; // { studentName, questionsAnswered, score }
    console.log('Received Score:', scoreData);

    const scores = readJSON(SCORES_DB);
    scores.push({
        ...scoreData,
        timestamp: new Date().toISOString()
    });
    writeJSON(SCORES_DB, scores);

    res.json({ message: 'Score saved' });
});

// Get Scores (Called by Dashboard)
app.get('/api/scores', (req, res) => {
    // In real app, check for teacher session/token here
    const scores = readJSON(SCORES_DB);
    res.json(scores);
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
