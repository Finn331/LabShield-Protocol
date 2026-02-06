#!/bin/bash

# Pastikan script berhenti jika ada error
set -e

echo "🚀 Starting LabShield Server..."

# Cek apakah docker terinstall
if ! command -v docker &> /dev/null
then
    echo "❌ Error: Docker tidak ditemukan. Harap install Docker terlebih dahulu."
    exit 1
fi

# Jalankan container
docker compose up -d --build --remove-orphans

echo "✅ Server berhasil dijalankan!"
echo "📡 Dashboard: http://localhost:5000/dashboard.html"
