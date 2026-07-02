@echo off
setlocal

REM Neu co virtualenv cuc bo (reranker\.venv), dung no. Neu khong, dung python/uvicorn
REM co san tren PATH. Khong phu thuoc vao conda hay bat ky duong dan ca nhan nao.
if exist "%~dp0.venv\Scripts\activate.bat" (
    call "%~dp0.venv\Scripts\activate.bat"
)

uvicorn main:app --port 8000
