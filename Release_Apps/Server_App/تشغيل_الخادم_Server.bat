@echo off
title SyncPulse Central Server (IOCP Raw Sockets 8888/8889/8887)
color 0A
echo ===================================================
echo   SyncPulse Central Server - Starting...
echo   Listening on TCP 8888, UDP 8889, UDP 8887
echo ===================================================
cd /d "%~dp0"
start SyncPulse.Server.exe
exit
