const express = require('express');
const bodyParser = require('body-parser');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const nodemailer = require('nodemailer');
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

// Email OTP config
const OTP_TTL_MS = 10 * 60 * 1000; // 10 minutes
const OTP_RESEND_COOLDOWN_MS = 60 * 1000; // 60 seconds
const OTP_MAX_VERIFY_ATTEMPTS = 5;
const OTP_SECRET = process.env.OTP_SECRET || 'labshield-otp-secret-change-this';

const SMTP_HOST = process.env.SMTP_HOST || '';
const SMTP_PORT = Number(process.env.SMTP_PORT || 587);
const SMTP_SECURE = String(process.env.SMTP_SECURE || 'false').toLowerCase() === 'true';
const SMTP_USER = process.env.SMTP_USER || '';
const SMTP_PASS = process.env.SMTP_PASS || '';
const SMTP_FROM = process.env.SMTP_FROM || '';

const pendingOtps = new Map(); // key: normalized email

const cleanupExpiredOtps = () => {
    const now = Date.now();
    for (const [email, state] of pendingOtps.entries()) {
        if (!state || now > Number(state.expiresAt || 0)) {
            pendingOtps.delete(email);
        }
    }
};

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

const normalizeUsername = (value) => String(value || '').trim();
const normalizeEmail = (value) => String(value || '').trim().toLowerCase();
const isValidEmail = (email) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

const findUserByUsername = (users, username) => {
    const normalized = normalizeUsername(username).toLowerCase();
    return users.find(u => String(u.username || '').trim().toLowerCase() === normalized);
};

const findUserByEmail = (users, email) => {
    const normalized = normalizeEmail(email);
    return users.find(u => normalizeEmail(u.email) === normalized);
};

const findUserByLoginIdentifier = (users, identifier) => {
    const normalized = normalizeUsername(identifier).toLowerCase();
    return users.find(u =>
        String(u.username || '').trim().toLowerCase() === normalized ||
        normalizeEmail(u.email) === normalized
    );
};

const createOtpCode = () => String(crypto.randomInt(100000, 1000000));
const hashOtp = (email, otp) => crypto
    .createHash('sha256')
    .update(`${normalizeEmail(email)}|${otp}|${OTP_SECRET}`)
    .digest('hex');

const isSmtpConfigured = () => Boolean(SMTP_HOST && SMTP_USER && SMTP_PASS && SMTP_FROM);

const createTransporter = () => {
    if (!isSmtpConfigured()) return null;
    return nodemailer.createTransport({
        host: SMTP_HOST,
        port: SMTP_PORT,
        secure: SMTP_SECURE,
        auth: {
            user: SMTP_USER,
            pass: SMTP_PASS
        }
    });
};

const sendOtpEmail = async (toEmail, otpCode) => {
    const transporter = createTransporter();
    if (!transporter) {
        throw new Error('SMTP belum dikonfigurasi di server.');
    }

    await transporter.sendMail({
        from: SMTP_FROM,
        to: toEmail,
        subject: 'Kode OTP Registrasi LabShield',
        text: `Kode OTP Anda: ${otpCode}\nKode berlaku 10 menit.\nJangan bagikan kode ini ke siapa pun.`,
        html: `
            <div style="font-family:Arial,sans-serif;line-height:1.5">
                <h2>LabShield Protocol</h2>
                <p>Gunakan kode OTP berikut untuk menyelesaikan registrasi:</p>
                <p style="font-size:28px;font-weight:700;letter-spacing:4px;margin:10px 0">${otpCode}</p>
                <p>Kode berlaku selama <b>10 menit</b>.</p>
                <p>Jangan bagikan kode ini kepada siapa pun.</p>
            </div>
        `
    });
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

const normalizeQuestionTimes = (questionTimes) => {
    if (!Array.isArray(questionTimes)) return [];

    return questionTimes.map((item) => {
        const questionID = String(item?.questionID || '').trim();
        const timeTakenSeconds = Number(item?.timeTakenSeconds ?? item?.timeTaken ?? 0);
        const isCorrect = Boolean(item?.isCorrect);

        return {
            questionID,
            timeTakenSeconds: Number.isFinite(timeTakenSeconds) ? Math.max(0, timeTakenSeconds) : 0,
            isCorrect
        };
    });
};

const computeQuizDurationSeconds = (questionTimes) => {
    const normalized = normalizeQuestionTimes(questionTimes);
    return normalized.reduce((sum, item) => sum + Number(item.timeTakenSeconds || 0), 0);
};

const normalizeScoreRow = (row) => {
    const apdTotalCorrect = Number(row.apdTotalCorrect || 0);
    const apdTotalWrong = Number(row.apdTotalWrong || 0);
    const quizTotalCorrect = Number(row.quizTotalCorrect || 0);
    const quizTotalWrong = Number(row.quizTotalWrong || 0);
    const apdTimeTakenSeconds = Number(row.apdTimeTakenSeconds || 0);
    const questionTimes = normalizeQuestionTimes(row.questionTimes);
    const quizTimeTakenSeconds = computeQuizDurationSeconds(questionTimes);

    const finalScoreStandard = computeStandardScore(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);
    const finalScoreK3 = computeK3Score(apdTotalCorrect, apdTotalWrong, quizTotalCorrect, quizTotalWrong);

    return {
        ...row,
        apdTotalCorrect,
        apdTotalWrong,
        apdTimeTakenSeconds,
        quizTotalCorrect,
        quizTotalWrong,
        questionTimes,
        quizTimeTakenSeconds,
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

const toSortableTime = (value) => {
    const t = Date.parse(value || 0);
    return Number.isNaN(t) ? 0 : t;
};

const toLeaderboardRow = (entry) => ({
    studentName: entry.studentName,
    attempts: entry.attempts,
    avgStandard: entry.avgStandard,
    avgK3: entry.avgK3,
    bestStandard: entry.bestStandard,
    bestK3: entry.bestK3,
    avgApdTimeSeconds: entry.avgApdTimeSeconds,
    avgQuizTimeSeconds: entry.avgQuizTimeSeconds,
    totalCorrect: entry.totalCorrect,
    totalWrong: entry.totalWrong,
    lastSubmitAt: entry.lastSubmitAt
});

const buildLeaderboardData = (scoresRaw) => {
    const scores = normalizeAttemptNumbers((Array.isArray(scoresRaw) ? scoresRaw : []).map(normalizeScoreRow));
    const students = new Map();

    for (const row of scores) {
        const studentName = String(row.studentName || '').trim();
        if (!studentName) continue;

        const key = studentName.toLowerCase();
        const scoreStandard = Number.isFinite(Number(row.finalScoreStandard))
            ? Number(row.finalScoreStandard)
            : Number(row.finalScore || 0);
        const scoreK3 = Number.isFinite(Number(row.finalScoreK3))
            ? Number(row.finalScoreK3)
            : computeK3Score(
                Number(row.apdTotalCorrect || 0),
                Number(row.apdTotalWrong || 0),
                Number(row.quizTotalCorrect || 0),
                Number(row.quizTotalWrong || 0)
            );
        const apdDuration = Math.max(0, Number(row.apdTimeTakenSeconds || 0));
        const quizDuration = Math.max(0, Number(row.quizTimeTakenSeconds || computeQuizDurationSeconds(row.questionTimes)));
        const totalCorrect = Number(row.apdTotalCorrect || 0) + Number(row.quizTotalCorrect || 0);
        const totalWrong = Number(row.apdTotalWrong || 0) + Number(row.quizTotalWrong || 0);
        const ts = toSortableTime(row.timestamp);

        if (!students.has(key)) {
            students.set(key, {
                studentName,
                attempts: 0,
                totalStandard: 0,
                totalK3: 0,
                bestStandard: 0,
                bestK3: 0,
                totalApdTimeSeconds: 0,
                totalQuizTimeSeconds: 0,
                totalCorrect: 0,
                totalWrong: 0,
                lastSubmitAt: row.timestamp || null,
                lastSubmitSortable: ts
            });
        }

        const entry = students.get(key);
        entry.attempts += 1;
        entry.totalStandard += scoreStandard;
        entry.totalK3 += scoreK3;
        entry.bestStandard = Math.max(entry.bestStandard, scoreStandard);
        entry.bestK3 = Math.max(entry.bestK3, scoreK3);
        entry.totalApdTimeSeconds += apdDuration;
        entry.totalQuizTimeSeconds += quizDuration;
        entry.totalCorrect += totalCorrect;
        entry.totalWrong += totalWrong;

        if (ts >= entry.lastSubmitSortable) {
            entry.lastSubmitSortable = ts;
            entry.lastSubmitAt = row.timestamp || entry.lastSubmitAt;
        }
    }

    const rows = Array.from(students.values()).map((entry) => {
        const attempts = Math.max(1, Number(entry.attempts || 0));
        return {
            studentName: entry.studentName,
            attempts: entry.attempts,
            avgStandard: clamp01to100(entry.totalStandard / attempts),
            avgK3: clamp01to100(entry.totalK3 / attempts),
            bestStandard: clamp01to100(entry.bestStandard),
            bestK3: clamp01to100(entry.bestK3),
            avgApdTimeSeconds: Math.max(0, entry.totalApdTimeSeconds / attempts),
            avgQuizTimeSeconds: Math.max(0, entry.totalQuizTimeSeconds / attempts),
            totalCorrect: Math.max(0, entry.totalCorrect),
            totalWrong: Math.max(0, entry.totalWrong),
            lastSubmitAt: entry.lastSubmitAt,
            lastSubmitSortable: entry.lastSubmitSortable
        };
    });

    const withRank = (list) => list.map((entry, idx) => ({ rank: idx + 1, ...toLeaderboardRow(entry) }));

    const overall = withRank([...rows].sort((a, b) =>
        b.avgK3 - a.avgK3 ||
        b.avgStandard - a.avgStandard ||
        b.totalCorrect - a.totalCorrect ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    const k3 = withRank([...rows].sort((a, b) =>
        b.avgK3 - a.avgK3 ||
        b.bestK3 - a.bestK3 ||
        b.avgStandard - a.avgStandard ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    const standard = withRank([...rows].sort((a, b) =>
        b.avgStandard - a.avgStandard ||
        b.bestStandard - a.bestStandard ||
        b.avgK3 - a.avgK3 ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    const fastestQuiz = withRank([...rows].sort((a, b) =>
        a.avgQuizTimeSeconds - b.avgQuizTimeSeconds ||
        b.avgK3 - a.avgK3 ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    const fastestApd = withRank([...rows].sort((a, b) =>
        a.avgApdTimeSeconds - b.avgApdTimeSeconds ||
        b.avgK3 - a.avgK3 ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    const mostActive = withRank([...rows].sort((a, b) =>
        b.attempts - a.attempts ||
        b.avgK3 - a.avgK3 ||
        b.lastSubmitSortable - a.lastSubmitSortable
    ));

    return {
        generatedAt: new Date().toISOString(),
        summary: {
            studentCount: rows.length,
            totalAttempts: scores.length
        },
        rankings: {
            overall,
            k3,
            standard,
            fastestQuiz,
            fastestApd,
            mostActive
        }
    };
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

// Request OTP (Student Registration)
app.post('/api/register/request-otp', loginLimiter, async (req, res) => {
    const email = normalizeEmail(req.body?.email);
    if (!isValidEmail(email)) {
        return res.status(400).json({ success: false, error: 'Email tidak valid.' });
    }

    if (!isSmtpConfigured()) {
        return res.status(500).json({
            success: false,
            error: 'Layanan email OTP belum aktif. Hubungi admin untuk konfigurasi SMTP.'
        });
    }

    const users = readJSON(USERS_DB);
    if (findUserByEmail(users, email)) {
        return res.status(400).json({ success: false, error: 'Email sudah terdaftar.' });
    }

    const now = Date.now();
    const existing = pendingOtps.get(email);
    if (existing && (now - existing.createdAt) < OTP_RESEND_COOLDOWN_MS) {
        const waitSeconds = Math.ceil((OTP_RESEND_COOLDOWN_MS - (now - existing.createdAt)) / 1000);
        return res.status(429).json({
            success: false,
            error: `Tunggu ${waitSeconds} detik sebelum meminta OTP lagi.`
        });
    }

    const otpCode = createOtpCode();
    const otpHash = hashOtp(email, otpCode);
    pendingOtps.set(email, {
        otpHash,
        createdAt: now,
        expiresAt: now + OTP_TTL_MS,
        attempts: 0,
        requestIp: req.ip
    });

    try {
        await sendOtpEmail(email, otpCode);
        return res.json({
            success: true,
            message: 'OTP sudah dikirim ke email. Silakan cek inbox/spam.'
        });
    } catch (error) {
        pendingOtps.delete(email);
        console.error('Failed to send OTP email:', error.message);
        return res.status(500).json({
            success: false,
            error: 'Gagal mengirim OTP ke email. Coba lagi nanti.'
        });
    }
});

// Register (Student Only - Public) with Email OTP verification
app.post('/api/register', loginLimiter, (req, res) => {
    const username = normalizeUsername(req.body?.username);
    const password = String(req.body?.password || '');
    const email = normalizeEmail(req.body?.email);
    const otp = String(req.body?.otp || '').trim();

    if (!username || !password || !email || !otp) {
        return res.status(400).json({ success: false, error: 'Username, password, email, dan OTP wajib diisi.' });
    }

    if (!isValidEmail(email)) {
        return res.status(400).json({ success: false, error: 'Email tidak valid.' });
    }

    const users = readJSON(USERS_DB);
    if (findUserByUsername(users, username)) {
        return res.status(400).json({ success: false, error: 'Username sudah digunakan.' });
    }
    if (findUserByEmail(users, email)) {
        return res.status(400).json({ success: false, error: 'Email sudah terdaftar.' });
    }

    const otpState = pendingOtps.get(email);
    if (!otpState) {
        return res.status(400).json({ success: false, error: 'OTP tidak ditemukan. Silakan kirim OTP terlebih dahulu.' });
    }

    const now = Date.now();
    if (now > otpState.expiresAt) {
        pendingOtps.delete(email);
        return res.status(400).json({ success: false, error: 'OTP sudah kedaluwarsa. Silakan minta OTP baru.' });
    }

    if (otpState.attempts >= OTP_MAX_VERIFY_ATTEMPTS) {
        pendingOtps.delete(email);
        return res.status(429).json({ success: false, error: 'Percobaan OTP terlalu banyak. Minta OTP baru.' });
    }

    const isMatch = hashOtp(email, otp) === otpState.otpHash;
    if (!isMatch) {
        otpState.attempts += 1;
        pendingOtps.set(email, otpState);
        return res.status(400).json({ success: false, error: 'OTP salah.' });
    }

    // In a real app, HASH the password!
    users.push({
        username,
        password,
        email,
        role: 'student',
        emailVerified: true,
        createdAt: new Date().toISOString()
    });
    writeJSON(USERS_DB, users);
    pendingOtps.delete(email);

    console.log(`User registered with verified email: ${username} (${email})`);
    res.json({ success: true, message: 'Registrasi berhasil. Email sudah terverifikasi.' });
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

    const user = findUserByLoginIdentifier(users, username);

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
        questionTimes: normalizeQuestionTimes(scoreData.questionTimes),
        quizTimeTakenSeconds: computeQuizDurationSeconds(scoreData.questionTimes),
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

// Get Leaderboard (aggregated by student)
// Query params:
// - limit: jumlah data per kategori (default 10, max 100)
app.get('/api/leaderboard', (req, res) => {
    const limitRaw = Number(req.query.limit || 10);
    const limit = Math.max(1, Math.min(100, Number.isFinite(limitRaw) ? Math.floor(limitRaw) : 10));
    const username = String(req.query.username || '').trim().toLowerCase();

    const scores = normalizeAttemptNumbers(readJSON(SCORES_DB).map(normalizeScoreRow));
    writeJSON(SCORES_DB, scores);

    const leaderboard = buildLeaderboardData(scores);
    const pickTop = (rows) => (Array.isArray(rows) ? rows.slice(0, limit) : []);
    const findSelf = (rows) => {
        if (!username || !Array.isArray(rows)) return null;
        const item = rows.find((row) => String(row.studentName || '').trim().toLowerCase() === username);
        return item || null;
    };

    res.json({
        generatedAt: leaderboard.generatedAt,
        summary: leaderboard.summary,
        limit,
        self: {
            overall: findSelf(leaderboard.rankings.overall),
            k3: findSelf(leaderboard.rankings.k3),
            standard: findSelf(leaderboard.rankings.standard),
            fastestQuiz: findSelf(leaderboard.rankings.fastestQuiz),
            fastestApd: findSelf(leaderboard.rankings.fastestApd),
            mostActive: findSelf(leaderboard.rankings.mostActive)
        },
        rankings: {
            overall: pickTop(leaderboard.rankings.overall),
            k3: pickTop(leaderboard.rankings.k3),
            standard: pickTop(leaderboard.rankings.standard),
            fastestQuiz: pickTop(leaderboard.rankings.fastestQuiz),
            fastestApd: pickTop(leaderboard.rankings.fastestApd),
            mostActive: pickTop(leaderboard.rankings.mostActive)
        }
    });
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

    // Cleanup OTP cache periodically
    setInterval(cleanupExpiredOtps, 60 * 1000);
});
