@echo off
set IMAGE_NAME=carlomuzaqi/labshield-server
set TAG=latest

echo ==============================================
echo  LabShield Server: Build and Push
echo ==============================================
echo.

echo [1/3] Building Docker Image...
docker build -t %IMAGE_NAME%:%TAG% .
if %errorlevel% neq 0 exit /b %errorlevel%

echo.
echo [2/3] Pushing to Docker Hub (%IMAGE_NAME%:%TAG%)...
docker push %IMAGE_NAME%:%TAG%
if %errorlevel% neq 0 (
    echo.
    echo ❌ Gagal Push! Pastikan kamu sudah 'docker login'
    exit /b 1
)

echo.
echo [3/3] Selesai!
echo Sekarang jalankan 'update-server' (atau ./start.sh) di server Debian kamu.
pause
