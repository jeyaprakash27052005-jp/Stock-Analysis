"""
Run the site on your PC:  python run_local.py   then open http://127.0.0.1:8000
Settings are read from a .env file in this folder (see .env.example).
"""
import os

here = os.path.dirname(os.path.abspath(__file__))
env = os.path.join(here, ".env")
if os.path.exists(env):
    for line in open(env, encoding="utf-8"):
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"'))

import uvicorn                               # noqa: E402
from fastapi.responses import FileResponse   # noqa: E402

from api.index import app                    # noqa: E402


@app.get("/")
def home():
    return FileResponse(os.path.join(here, "index.html"))


if __name__ == "__main__":
    print("Open http://127.0.0.1:8000 in your browser")
    uvicorn.run(app, host="127.0.0.1", port=8000)
