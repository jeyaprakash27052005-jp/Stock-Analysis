@echo off
cd /d "%~dp0"
if not exist "requirements.txt" (echo Extract the ZIP first, then run this file from the extracted folder.& pause & exit /b)
set PY=python
for %%V in (3.13 3.12 3.11) do (py -%%V -c "import sys" >nul 2>&1 && (set PY=py -%%V& goto found))
:found
%PY% -m pip install -q --disable-pip-version-check -r requirements-local.txt
start "" http://127.0.0.1:8000
%PY% run_local.py
pause
