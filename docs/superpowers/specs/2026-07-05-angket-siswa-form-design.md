# Angket Respon Peserta Didik — Design Spec

## Goal
Buat halaman angket (kuesioner) 20 pernyataan dengan skala SS/S/TS/STS untuk siswa yang sudah login. Setiap siswa hanya bisa mengisi satu kali.

## Title & Description
- **Judul:** ANGKET RESPON PESERTA DIDIK PADA GAME EDUKASI BERBASIS APLIKASI UNTUK MENINGKATKAN PEMAHAMAN K3LH DI LABORATORIUM KIMIA SEKOLAH
- **Petunjuk:**
  1. Bacalah setiap pernyataan dengan teliti.
  2. Pilih salah satu jawaban yang tersedia.
  3. Berikan jawaban sesuai dengan pendapat Anda.
- **Skala Penilaian:**
  - SS = Sangat Setuju (skor 4)
  - S = Setuju (skor 3)
  - TS = Tidak Setuju (skor 2)
  - STS = Sangat Tidak Setuju (skor 1)

## 20 Pernyataan
1. Materi dalam game dan website mempermudah saya dalam mempelajari dan memahami konsep K3LH.
2. Game ini memberikan wawasan baru serta inovasi dalam pembelajaran kimia bagi saya.
3. Game ini memberikan pengalaman belajar yang kontekstual atau sesuai dengan situasi nyata.
4. Permasalahan dalam game membantu saya dalam memecahkan masalah lingkungan sekitar laboratorium.
5. Tersedia bahan bacaan dan video pengenalan K3LH yang membantu peserta didik lebih memahami konsep materi yang disampaikan.
6. Tersedia skor dalam game yang membantu saya mengetahui tingkat kemampuan diri saya sendiri.
7. Desain game yang menampilkan umpan balik saat jawaban salah membantu saya mengevaluasi dan memahami jawaban yang benar.
8. Latihan soal dengan kasus nyata membantu saya memperdalam pemahaman terhadap materi yang disajikan.
9. Tampilan game aplikasi 3D berbantuan website membuat saya tertarik untuk mengikuti pembelajaran kimia.
10. Kualitas gambar, warna, ukuran, dan video pendukung dalam aplikasi berbantuan website menarik dan sesuai.
11. Media ajar menggunakan aplikasi yang dikembangkan dalam bentuk media game mempermudah saya dalam memahami konsep K3LH.
12. Game menggunakan bahasa yang komunikatif sehingga mudah dipahami.
13. Penggunaan kalimat dalam game berbantuan website telah sesuai dengan kaidah tata bahasa Indonesia yang baik dan benar.
14. Ketepatan penulisan dan animasi dalam game dan website mudah dipahami oleh peserta didik.
15. Game dapat dengan mudah diakses dan digunakan melalui perangkat handphone.
16. Secara keseluruhan, game K3LH ini bermanfaat dalam pembelajaran.
17. Game memberikan pengalaman belajar yang menyenangkan.
18. Game ini membantu saya mengetahui tindakan yang benar saat bekerja di laboratorium.
19. Game membantu saya menghubungkan materi K3LH dengan praktik di laboratorium.
20. Alur permainan dalam game mudah diikuti dari awal hingga akhir.

## Architecture

### Data Storage
- File: `Server/angket_responses.json` (array of objects)
- Struktur per entry:
```json
{
  "username": "amanda_azzahra",
  "jawaban": [4, 3, 4, 2, 4, 3, 3, 4, 2, 4, 3, 4, 3, 4, 2, 4, 4, 3, 4, 3],
  "timestamp": "2026-07-05T12:00:00.000Z"
}
```
Skor: 4=SS, 3=S, 2=TS, 1=STS

### Backend Endpoints
- `GET /api/angket/status?username=...` → `{ filled: true/false }`
- `POST /api/angket/submit` → body: `{ username, jawaban: [20 angka 1-4] }` → return success/error
  - Validasi: username wajib, jawaban array 20 angka (1-4)
  - Cek duplikat: jika username sudah ada, return 403 "Sudah pernah mengisi angket"
  - Jika valid: simpan + timestamp, return 200

### Frontend Pages
- `Server/public/angket.html` — halaman form
- Tidak perlu halaman sukses terpisah; cukup tampilkan pesan sukses di halaman yang sama

### Student Dashboard Changes
- `Server/public/student-dashboard.html` — tambah tombol "Isi Angket" di section dashboard setelah login
- Tombol hanya muncul jika siswa belum mengisi (cek via `/api/angket/status`)
- Link mengarah ke `angket.html?username=...`

### UI Design (menggunakan 21st-dev-magic + style.css existing)
- Header dengan judul panjang
- Petunjuk pengisian + skala
- Tabel: No | Pernyataan | STS | TS | S | SS
- Radio button per opsi
- Tombol submit
- Validasi client-side: semua harus diisi
- Loading state saat submit

### One-Time Enforcement Flow
1. Saat halaman angket dimuat → `GET /api/angket/status?username=XXX`
2. Jika `filled: true` → tampilkan pesan "Kamu sudah mengisi angket ini" + sembunyikan form
3. Jika `filled: false` → tampilkan form
4. Saat submit → `POST /api/angket/submit`
5. Server cek duplikat lagi untuk keamanan
